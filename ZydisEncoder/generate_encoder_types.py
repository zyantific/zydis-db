#!/usr/bin/env python3
import os
import io
import argparse
from pcpp import Preprocessor
from pycparser import c_parser, c_ast


class ZydisPreprocessor(Preprocessor):

    def __init__(self, zydis_path):
        super().__init__()
        self.zydis_path = zydis_path
        self.add_path(os.path.join(zydis_path, 'dependencies', 'zycore', 'include'))
        self.add_path(os.path.join(zydis_path, 'include'))
        self.line_directive = None
        self.define('ZYAN_NO_LIBC')
        self.define('__x86_64__')
        self.define('__GNUC__ 6')
        self.define('__STDC_VERSION__ 201112L')
        self.define('__builtin_va_list char*')
        self.define('__UINT8_TYPE__ unsigned char')
        self.define('__UINT16_TYPE__ unsigned short')
        self.define('__UINT32_TYPE__ unsigned int')
        self.define('__UINT64_TYPE__ unsigned long long')
        self.define('__INT8_TYPE__ signed char')
        self.define('__INT16_TYPE__ signed short')
        self.define('__INT32_TYPE__ signed int')
        self.define('__INT64_TYPE__ signed long long')
        self.define('__SIZE_TYPE__ unsigned long long')
        self.define('__PTRDIFF_TYPE__ long long')
        self.define('__UINTPTR_TYPE__ unsigned long long')
        self.define('__INTPTR_TYPE__ long long')
        with open(os.path.join(zydis_path, 'src', 'Encoder.c')) as f:
            self.parse(f.read())

    def on_file_open(self, is_system_include, includepath):
        if is_system_include and os.path.basename(includepath) == 'ZydisExportConfig.h':
            return open(os.path.join(self.zydis_path, 'assets', 'ZydisExportConfigSample.h'))

        return super().on_file_open(is_system_include, includepath)

    def get_preprocessed_file(self):
        file = io.StringIO()
        self.write(file)
        file.seek(0)
        return file.read()

    @staticmethod
    def concatenate_tokens(tokens):
        return ''.join([token.value for token in tokens])

    def expand_macro(self, macro):
        if isinstance(macro, str):
            macro = self.macros[macro].value
        else:
            macro = macro.value
        return self.concatenate_tokens(self.expand_macros(macro))


class ZydisParser:

    class ZydisParsingError(Exception):
        pass

    class EnumVisitor(c_ast.NodeVisitor):

        def __init__(self, typenames):
            self.typenames = typenames
            self.parsed_types = {}

        @staticmethod
        def get_const_value(node):
            if node is None:
                return None
            if node.type != 'int':
                raise ZydisParser.ZydisParsingError('Got enum value of type %s (expected int)' % node.type)
            if node.value.startswith('0x'):
                return int(node.value, 16)
            else:
                return int(node.value)

        def visit_Typedef(self, node):
            if node.name in self.typenames:
                self.parsed_types[node.name] = [
                    (e.name, self.get_const_value(e.value))
                    for e in node.type.type.values.enumerators
                    if not e.name.endswith('_MAX_VALUE') and not e.name.endswith('_REQUIRED_BITS')
                ]

    def __init__(self, file, filename):
        parser = c_parser.CParser()
        self.ast = parser.parse(file, filename=filename)

    def get_enum_types(self, typenames):
        v = ZydisParser.EnumVisitor(typenames)
        v.visit(self.ast)
        types = ''
        for typename in typenames:
            if typename not in v.parsed_types:
                raise ZydisParser.ZydisParsingError('Type %s not found' % typename)
            enumerators = v.parsed_types[typename]
            value_type = type(enumerators[0][1])
            if not all([isinstance(e[1], value_type) for e in enumerators]):
                raise ZydisParser.ZydisParsingError('%s: All values must be explicitly assigned or no values must be assigned' % typename)

            is_flag_enum = False
            if value_type == int:
                popcnts = [bin(e[1]).count('1') == 1 for e in enumerators]
                is_flag_enum = not popcnts[0] and all(popcnts[1:]) and \
                               [e[1] for e in enumerators][1:] == [2**n for n in range(len(enumerators) - 1)]
            if is_flag_enum:
                types += "%s = IntFlag('%s', [\n" % (typename, typename)
                for e in enumerators:
                    types += "    '%s',\n" % e[0]
                types += '], start=0)\n\n\n'
            else:
                types += "%s = IntEnum('%s', [\n" % (typename, typename)
                if isinstance(None, value_type):
                    for e in enumerators:
                        types += "    '%s',\n" % e[0]
                    types += '], start=0)\n\n\n'
                else:
                    for e in enumerators:
                        types += "    ('%s', %d),\n" % e
                    types += '])\n\n\n'

        return types


if __name__ == "__main__":
    arg_parser = argparse.ArgumentParser(description='Generates python versions of Zydis types from C sources')
    arg_parser.add_argument('zydis_path', nargs='?', default=os.path.join('..', '..', 'zydis'), help='Path to Zydis repository')
    args = arg_parser.parse_args()

    # Preprocess Zydis sources for parsing
    preprocessor = ZydisPreprocessor(args.zydis_path)
    encoder_file = preprocessor.get_preprocessed_file()

    # Extract important #define constants
    zydis_encoder_max_operands = preprocessor.expand_macro('ZYDIS_ENCODER_MAX_OPERANDS')
    zydis_attributes = """ZydisInstructionAttributes = IntFlag('ZydisInstructionAttributes', [
    ('ZYDIS_ATTRIB_NONE', 0),\n"""
    attributes = [
        (k, preprocessor.expand_macro(v).replace('ULL', ''))
        for k, v in preprocessor.macros.items()
        if k.startswith('ZYDIS_ATTRIB_')
    ]
    attributes.sort(key=lambda x: eval(x[1]))
    attributes = list(filter(lambda x: bin(eval(x[1])).count('1') == 1, attributes))
    zydis_attributes += '\n'.join("    ('%s', %s)," % x for x in attributes)
    zydis_attributes += '\n])\n\n\n'
    zydis_encodable_prefixes = 'ZYDIS_ENCODABLE_PREFIXES = '
    parts = preprocessor.concatenate_tokens(preprocessor.macros['ZYDIS_ENCODABLE_PREFIXES'].value) \
        .replace('(', '') \
        .replace(')', '') \
        .replace(' ', '') \
        .split('|')
    joined_parts = '\n'.join(['%sZydisInstructionAttributes.%s | \\' % (' ' * len(zydis_encodable_prefixes), p) for p in parts])
    zydis_encodable_prefixes += '%s\n\n\n' % joined_parts[len(zydis_encodable_prefixes):-4]

    # Parse sources and extract enum types
    parser = ZydisParser(encoder_file, 'Encoder.c')
    enums = parser.get_enum_types([
        'ZydisMachineMode',
        'ZydisStackWidth',
        'ZydisBranchType',
        'ZydisBranchWidth',
        'ZydisEncodableEncoding',
        'ZydisAddressSizeHint',
        'ZydisOperandSizeHint',
        'ZydisBroadcastMode',
        'ZydisRoundingMode',
        'ZydisConversionMode',
        'ZydisSwizzleMode',
        'ZydisOperandType',
        'ZydisMnemonic',
        'ZydisRegister',
    ])

    # Save generated file
    with open(os.path.join(args.zydis_path, 'tests', 'zydis_encoder_types.py'), 'w') as f:
        f.write("""#!/usr/bin/env python3
from enum import IntEnum, IntFlag


ZYDIS_ENCODER_MAX_OPERANDS = %s
SIZE_OF_ZYDIS_ENCODER_OPERAND = 64  # This value must be corrected manually if structure layout changes\n\n\n""" % zydis_encoder_max_operands)
        f.write(zydis_attributes)
        f.write(zydis_encodable_prefixes)
        f.write(enums)
