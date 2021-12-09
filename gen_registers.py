#!/usr/bin/env python3
# encoding: utf-8

import json
import os

class _JsonRegisters():

    def __init__(self, categorized, uncategorized):
        self.categorized = list(map(lambda x: _JsonRegistersCategorized(**x), categorized))
        self.uncategorized = uncategorized

class _JsonRegistersCategorized():

    def __init__(self, name_zydis, name_editor, description, width, width_64, **kwargs):
        self.name_zydis = name_zydis
        self.name_editor = name_editor
        self.description = description
        self.encodable = kwargs.get('encodable', None)   
        self.unique = kwargs.get('unique', None)  
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
        self.__file_name = file_name
        self.__symbol_name = symbol_name
        self.__item_type = item_type
        self.__static = 'static ' if is_static else ''
        self.__const = 'const ' if is_const else ''
        self.__indent = ' ' * indent
        self.__expect_delim = False
        
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
            '{static}{const}{item_type} {symbol_name}[] =\n'
            '{{\n'
        ).format(
            static = self.__static,
            const = self.__const,
            item_type = self.__item_type,
            symbol_name = self.__symbol_name
        )
        self.__f.write(header)

    def __emit_footer(self):
        footer = (
            '\n};\n'
        )
        self.__f.write(footer)

    def __emit_delim(self):
        if self.__expect_delim:
            self.__f.write(',\n')

    def emit_value(self, value):
        self.__emit_delim()
        self.__f.write('{indent}{value}'.format(
            indent = self.__indent, 
            value = value
        ))
        self.__expect_delim = True

    def emit_comment(self, comment):
        self.__emit_delim()
        self.__f.write('{indent}// {comment}\n'.format(
            indent = self.__indent, 
            comment = comment
        ))
        self.__expect_delim = False

    def emit_newline(self):
        self.__emit_delim()
        self.__f.write('\n')
        self.__expect_delim = False 

class _ZydisStringTableWriter(_ZydisTableWriter):

    def __init__(self, symbol_name, make_shortstrings, file_name, indent=4):
        item_type = 'ZydisShortString' if make_shortstrings else 'char*'
        super().__init__(symbol_name, item_type, file_name, True, True, indent)
        self.__make_shortstrings = make_shortstrings
        
    def emit_value(self, value):
        value = '"{value}"'.format(
            value = value.lower()
        )
        if self.__make_shortstrings:
            value = 'ZYDIS_MAKE_SHORTSTRING({value})'.format(
                value = value
            )
        super().emit_value(value) 

class Generator():

    def __init__(self, input_file):
        with open(input_file) as f:
            dic = json.load(f)
            self.__registers = _JsonRegisters(**dic)

    def generate(self, output_dir):
        categorized = self.__registers.categorized
        uncategorized = self.__registers.uncategorized
        if (not categorized) or (not uncategorized):
            raise ValueError()

        self.__generate_enum(categorized, uncategorized, output_dir)
        self.__generate_reg_lookup(categorized, uncategorized, output_dir)
        self.__generate_reg_class_lookup(categorized, output_dir)

    def __generate_enum(self, categorized, uncategorized, output_dir):
        fn_enum = os.path.join(output_dir, 'include/Zydis/Generated/EnumRegister.h')
        fn_enum_strings = os.path.join(output_dir, 'src/Generated/EnumRegister.inc')
        with (
            _ZydisEnumWriter('Register', 'REGISTER', fn_enum) as w_enum, 
            _ZydisStringTableWriter('STR_REGISTER', True, fn_enum_strings) as w_enum_strings
        ):
            w_enum.emit_value('none')
            w_enum_strings.emit_value('none')

            for reg_class in categorized:
                if not reg_class.name_zydis:
                    continue
                w_enum.emit_newline()
                w_enum.emit_comment(reg_class.description)
                w_enum_strings.emit_comment(reg_class.description)
                if reg_class.encodable:
                    for reg in reg_class.encodable:
                        w_enum.emit_value(reg)
                        w_enum_strings.emit_value(reg)
                if reg_class.unique:
                    for reg in reg_class.unique:
                        w_enum.emit_value(reg) 
                        w_enum_strings.emit_value(reg)

            w_enum.emit_newline()
            w_enum.emit_comment("Uncategorized")
            w_enum_strings.emit_comment("Uncategorized")
            for reg in uncategorized:
                w_enum.emit_value(reg)
                w_enum_strings.emit_value(reg)

    def __generate_reg_lookup(self, categorized, uncategorized, output_dir):
        fn_table = os.path.join(output_dir, 'src/Generated/RegisterLookup.inc')
        with _ZydisTableWriter('REG_LOOKUP', 'ZydisRegisterLookupItem', fn_table) as w:
            w.emit_value('/* NONE       */ { ZYDIS_REGCLASS_INVALID, -1, 0, 0 }') # none
            for reg_class in categorized:
                if not reg_class.name_zydis:
                    continue
                # w.emit_comment(reg_class.description)
                if reg_class.encodable:
                    i = 0
                    for reg in reg_class.encodable:
                        w.emit_value('/* {reg} */ {{ ZYDIS_REGCLASS_{reg_class}, {id}, {width}, {width_64} }}'.format(
                            reg = reg.upper().ljust(10),
                            reg_class = reg_class.name_zydis,
                            id = i,
                            width = reg_class.width,
                            width_64 = reg_class.width_64
                        ))
                        i += 1
                if reg_class.unique:
                    for reg in reg_class.unique:
                        w.emit_value('/* {reg} */ {{ ZYDIS_REGCLASS_{reg_class}, -1, 0, 0 }}'.format(
                            reg = reg.upper().ljust(10),
                            reg_class = reg_class.name_zydis if not reg_class.encodable else 'INVALID'  
                        ))

            # w.emit_comment("Uncategorized")
            for reg in uncategorized:
                w.emit_value('/* {reg} */ {{ ZYDIS_REGCLASS_INVALID, -1, 0, 0 }}'.format(
                    reg = reg.upper().ljust(10)   
                ))

    def __generate_reg_class_lookup(self, categorized, output_dir):
        fn_table = os.path.join(output_dir, 'src/Generated/RegisterClassLookup.inc')
        with _ZydisTableWriter('REG_CLASS_LOOKUP', 'ZydisRegisterClassLookupItem', fn_table) as w:
            w.emit_value('/* INVALID */ { ZYDIS_REGISTER_NONE, ZYDIS_REGISTER_NONE, 0, 0 }') # none
            for reg_class in categorized:
                if not reg_class.name_zydis:
                    continue
                # w.emit_comment(reg_class.description)
                lo = 'ZYDIS_REGISTER_NONE'
                hi = 'ZYDIS_REGISTER_NONE'
                if reg_class.encodable:
                    lo = 'ZYDIS_REGISTER_' + reg_class.encodable[0]
                    hi = 'ZYDIS_REGISTER_' + reg_class.encodable[len(reg_class.encodable) - 1]
                w.emit_value('/* {reg_class} */ {{ {lo}, {hi}, {width}, {width_64} }}'.format(
                    reg_class = reg_class.name_zydis.ljust(7),
                    lo = lo,
                    hi = hi,
                    width = reg_class.width,
                    width_64 = reg_class.width_64
                ))
              
generator = Generator('./Data/registers.json')
generator.generate('F:/Development/GitHub/zydis/')
