#!/usr/bin/env python3
# encoding: utf-8

import json
import os
import sys

class _JsonRegisterClass():

    def __init__(self, name_zydis, name_editor, description, groups):
        self.name_zydis = name_zydis
        self.name_editor = name_editor
        self.description = description
        self.groups = list(map(lambda x: _JsonRegisterGroup(**x), groups))

class _JsonRegisterGroup():

    def __init__(self, is_encodable, registers, width, width_64):
        self.is_encodable = is_encodable
        self.registers = registers
        self.width = width
        self.width_64 = width_64   

class _ZydisEnumWriter():

    def __init__(self, enum_name, value_prefix, file_name, indent=4):
        self.__file_name = file_name
        self.__enum_name = 'Zydis{enum_name}'.format(
            enum_name = enum_name
        )
        self.__value_prefix = 'ZYDIS_{prefix}_'.format(
            prefix = value_prefix
        )
        self.__indent = ' ' * indent
        self.__last = None
        
    def __enter__(self):
        self.__f = open(self.__file_name, 'w')
        self.__emit_header()
        return self

    def __exit__(self, exc_type, exc_value, tb):
        if exc_type is not None:
            return False
        self.__emit_footer()
        self.__f.close()
        return True

    def __emit_header(self):
        header = (
            '/**\n'
            ' * Defines the `{enum_name}` enum.\n'
            ' */\n'
            'typedef enum {enum_name}_\n'
            '{{\n'
        ).format(
            enum_name = self.__enum_name
        )
        self.__f.write(header)

    def __emit_footer(self):
        if not self.__last:
            raise RuntimeError("Enum must contain at least one item")
        footer = (
            '\n'
            '{indent}/**\n'
            '{indent} * Maximum value of this enum.\n'
            '{indent} */\n'
            '{indent}{prefix}MAX_VALUE = {prefix}{last},\n'
            '{indent}/**\n'
            '{indent} * The minimum number of bits required to represent all values of this enum.\n'
            '{indent} */\n'
            '{indent}{prefix}REQUIRED_BITS = ZYAN_BITS_TO_REPRESENT({prefix}MAX_VALUE)\n'
            '}} {enum_name};\n'
        ).format(
            indent = self.__indent,
            enum_name = self.__enum_name,
            prefix = self.__value_prefix,
            last = self.__last
        )
        self.__f.write(footer)

    def emit_value(self, value):
        self.__f.write('{indent}{prefix}{value},\n'.format(
            indent = self.__indent, 
            prefix = self.__value_prefix,
            value = value.upper()
        ))
        self.__last = value

    def emit_comment(self, comment):
        self.__f.write('{indent}// {comment}\n'.format(
            indent = self.__indent, 
            comment = comment
        ))

    def emit_newline(self):
        self.__f.write('\n') 

class _ZydisTableWriter():
    def __init__(self, symbol_name, item_type, file_name, is_static=True, is_const=True, indent=4):
        self._file_name = file_name
        self.__symbol_name = symbol_name
        self.__item_type = item_type
        self.__static = 'static ' if is_static else ''
        self.__const = 'const ' if is_const else ''
        self.__indent = ' ' * indent
        self.__expect_delim = False
        
    def __enter__(self):
        self._f = open(self._file_name, 'w')
        self.__emit_header()
        return self

    def __exit__(self, exc_type, exc_value, tb):
        if exc_type is not None:
            return False
        self.__emit_footer()
        self._f.close()
        return True

    def __emit_header(self):
        header = (
            '{static}{const}{item_type} {symbol_name}[] =\n'
            '{{\n'
        ).format(
            static = self.__static,
            const = self.__const,
            item_type = self.__item_type,
            symbol_name = self.__symbol_name
        )
        self._f.write(header)

    def __emit_footer(self):
        footer = (
            '\n};\n'
        )
        self._f.write(footer)

    def __emit_delim(self):
        if self.__expect_delim:
            self._f.write(',\n')

    def emit_value(self, value):
        self.__emit_delim()
        self._f.write('{indent}{value}'.format(
            indent = self.__indent,
            value = value
        ))
        self.__expect_delim = True

    def emit_comment(self, comment, indent=None):
        self.__emit_delim()
        self._f.write('{indent}// {comment}\n'.format(
            indent = indent if indent is not None else self.__indent,
            comment = comment
        ))
        self.__expect_delim = False

    def emit_newline(self):
        self.__emit_delim()
        self._f.write('\n')
        self.__expect_delim = False

class _ZydisShortStringTableWriter(_ZydisTableWriter):

    def __init__(self, symbol_name, file_name, indent=4):
        super().__init__(symbol_name, 'ZydisShortString*', file_name, True, True, indent)
        self.__symbol_name = symbol_name
        self.__values_count = 0

    def __value_symbol_name(self, i):
        return f'{self.__symbol_name.removeprefix("STR_")}_VALUE_{i}'

    def __enter__(self):
        self._f = open(self._file_name, 'w')
        return self

    def __exit__(self, exc_type, exc_value, tb):
        self._f.write('\n')
        self._ZydisTableWriter__emit_header()
        for i in range(self.__values_count):
            super().emit_value(f'ZYDIS_SHORTSTRING({self.__value_symbol_name(i)})')
        return super().__exit__(exc_type, exc_value, tb)

    def emit_value(self, value):
        self._f.write('ZYDIS_MAKE_SHORTSTRING({sym}, "{value}");\n'.format(
            sym = self.__value_symbol_name(self.__values_count),
            value = value.lower()
        ))
        self.__values_count += 1

    def emit_comment(self, comment, indent=None):
        super().emit_comment(comment, indent=indent if indent is not None else '')

class Generator():

    def __init__(self, input_file):
        with open(input_file) as f:
            dic = json.load(f)
            self.__registers = list(map(lambda x: _JsonRegisterClass(**x), dic['classes']))

    def generate(self, output_dir):
        self.__generate_enum(self.__registers, output_dir)
        self.__generate_reg_lookup(self.__registers, output_dir)
        self.__generate_reg_class_lookup(self.__registers, output_dir)

    @staticmethod
    def __generate_enum(reg_classes, output_dir):
        fn_enum = os.path.join(output_dir, 'include/Zydis/Generated/EnumRegister.h')
        fn_enum_strings = os.path.join(output_dir, 'src/Generated/EnumRegister.inc')
        with (
            _ZydisEnumWriter('Register', 'REGISTER', fn_enum) as w_enum,
            _ZydisShortStringTableWriter('STR_REGISTERS', fn_enum_strings) as w_enum_strings
        ):
            w_enum.emit_value('none')
            w_enum_strings.emit_value('none')

            for reg_class in reg_classes:
                if not reg_class.name_zydis:
                    # This class is only used by the InstructionEditor
                    continue
                if reg_class.description:
                    w_enum.emit_newline()
                    w_enum.emit_comment(reg_class.description)
                    w_enum_strings.emit_comment(reg_class.description)
                for group in reg_class.groups:
                    for reg in group.registers:
                        w_enum.emit_value(reg)
                        w_enum_strings.emit_value(reg)

    @staticmethod
    def __generate_reg_lookup(reg_classes, output_dir):
        fn_table = os.path.join(output_dir, 'src/Generated/RegisterLookup.inc')
        with _ZydisTableWriter('REG_LOOKUP', 'ZydisRegisterLookupItem', fn_table) as w:
            w.emit_value('/* NONE       */ { ZYDIS_REGCLASS_INVALID, -1, 0, 0 }') # none
            for reg_class in reg_classes:
                if not reg_class.name_zydis:
                    # This class is only used by the InstructionEditor
                    continue
                for reg_group in reg_class.groups:
                    i = 0
                    for reg in reg_group.registers:
                        w.emit_value('/* {reg} */ {{ ZYDIS_REGCLASS_{reg_class}, {id}, {width}, {width_64} }}'.format(
                            reg = reg.upper().ljust(10),
                            reg_class = reg_class.name_zydis,
                            id = i if reg_group.is_encodable else -1,
                            width = reg_group.width,
                            width_64 = reg_group.width_64
                        ))
                        i += 1

    @staticmethod
    def __generate_reg_class_lookup(reg_classes, output_dir):
        fn_table = os.path.join(output_dir, 'src/Generated/RegisterClassLookup.inc')
        with _ZydisTableWriter('REG_CLASS_LOOKUP', 'ZydisRegisterClassLookupItem', fn_table) as w:
            w.emit_value('/* INVALID */ { ZYDIS_REGISTER_NONE, ZYDIS_REGISTER_NONE, 0, 0 }') # none
            for reg_class in reg_classes:
                if not reg_class.name_zydis:
                    # This class is only used by the InstructionEditor
                    continue
                if reg_class.name_zydis == 'INVALID':
                    # This class contains the assorted registers
                    continue
                lo = 'ZYDIS_REGISTER_NONE'
                hi = 'ZYDIS_REGISTER_NONE'
                width = 0
                width_64 = 0
                encodable_groups = list(filter(lambda x : x.is_encodable, reg_class.groups))
                if len(encodable_groups) > 0:
                    if len(encodable_groups) > 1:
                        raise ValueError("multiple encodable groups in register class")
                    encodable_group = encodable_groups[0]
                    lo = 'ZYDIS_REGISTER_' + encodable_group.registers[0]
                    hi = 'ZYDIS_REGISTER_' + encodable_group.registers[len(encodable_group.registers) - 1]
                    width = encodable_group.width
                    width_64 = encodable_group.width_64
                w.emit_value('/* {reg_class} */ {{ {lo}, {hi}, {width}, {width_64} }}'.format(
                    reg_class = reg_class.name_zydis.ljust(7),
                    lo = lo,
                    hi = hi,
                    width = width,
                    width_64 = width_64
                ))

if __name__ == "__main__":
    if len(sys.argv) != 3:
        sys.stderr.write(f"usage: {sys.argv[0]} [datafiles/registers.json] [path/to/zydis/]")
        sys.exit(1)
    generator = Generator(sys.argv[1])
    generator.generate(sys.argv[2])
