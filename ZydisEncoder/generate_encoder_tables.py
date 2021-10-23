#!/usr/bin/env python3
import json
import argparse
import copy
from utils import *
from zydis_types import *
from instruction_order import generate_instruction_order_test_table
from abc import ABC, abstractmethod


class InvalidInstructionException(Exception):
    pass


def get_instructions(unique_instructions, lookup):
    return (unique_instructions[i] for i in lookup)


def get_instruction_mode(insn):
    encoding = insn.get('encoding', 'default')
    filters = insn.get('filters', {})
    mode = filters.get('mode', 'placeholder')
    rex_w = filters.get('rex_w', 'placeholder')
    operand_size = filters.get('operand_size', 'placeholder')
    address_size = filters.get('address_size', 'placeholder')
    if mode == '!64':
        mode_mask = ZydisWidthFlag.w_16 | ZydisWidthFlag.w_32
        if rex_w == '1'and encoding not in ['vex', 'evex', 'xop']:
            raise InvalidInstructionException()
        if address_size == '64' or operand_size == '64':
            raise InvalidInstructionException()
    elif mode == '64':
        mode_mask = ZydisWidthFlag.w_64
        if address_size == '16':
            raise InvalidInstructionException()
    elif mode == 'placeholder':
        mode_mask = ZydisWidthFlag.w_16 | ZydisWidthFlag.w_32 | ZydisWidthFlag.w_64
        if address_size == '64':
            mode_mask = ZydisWidthFlag.w_64
        if operand_size == '64':
            mode_mask = ZydisWidthFlag.w_64
        if rex_w == '1' and encoding not in ['vex', 'evex', 'xop']:
            mode_mask = ZydisWidthFlag.w_64
    else:
        raise ValueError('Invalid mode: ' + mode)

    return mode_mask


def get_width_flags(flags):
    flag_strings = []
    if flags & ZydisWidthFlag.w_16:
        flag_strings.append('ZYDIS_WIDTH_16')
    if flags & ZydisWidthFlag.w_32:
        flag_strings.append('ZYDIS_WIDTH_32')
    if flags & ZydisWidthFlag.w_64:
        flag_strings.append('ZYDIS_WIDTH_64')
    if len(flag_strings) == 0:
        raise InvalidInstructionException()
    return ' | '.join(flag_strings)


def get_size_flags(expression):
    if expression == '16':
        return ZydisWidthFlag.w_16
    elif expression == '32':
        return ZydisWidthFlag.w_32
    elif expression == '64':
        return ZydisWidthFlag.w_64
    elif expression == 'placeholder':
        return ZydisWidthFlag.w_16 | ZydisWidthFlag.w_32 | ZydisWidthFlag.w_64
    elif expression == '!16':
        return ZydisWidthFlag.w_32 | ZydisWidthFlag.w_64
    elif expression == '!64':
        return ZydisWidthFlag.w_16 | ZydisWidthFlag.w_32
    else:
        raise ValueError('Invalid size expression: ' + expression)


def get_address_size(insn):
    address_size = insn.get('filters', {}).get('address_size', 'placeholder')
    return get_size_flags(address_size)


def get_operand_size(insn):
    operand_size = insn.get('filters', {}).get('operand_size', 'placeholder')
    return get_size_flags(operand_size)


def get_effective_address_size(insn):
    mask = ZydisWidthFlag.w_invalid
    if insn['allowed_modes'] & (ZydisWidthFlag.w_16 | ZydisWidthFlag.w_32):
        mask |= ZydisWidthFlag.w_16 | ZydisWidthFlag.w_32
    if insn['allowed_modes'] & ZydisWidthFlag.w_64:
        mask |= ZydisWidthFlag.w_32 | ZydisWidthFlag.w_64
    return insn['address_size'] & mask


def get_modrm(insn):
    if 'filters' not in insn:
        return 0
    modrm = 0
    filters = insn['filters']
    if filters.get('modrm_mod', '') == '3':
        modrm |= 3 << 6
    modrm |= int(filters.get('modrm_reg', '0')) << 3
    modrm |= int(filters.get('modrm_rm', '0'))
    return modrm


def get_mandatory_prefix(insn):
    if 'filters' not in insn or 'mandatory_prefix' not in insn['filters']:
        return 'ZYDIS_MANDATORY_PREFIX_NONE'
    mandatory_prefix = insn['filters']['mandatory_prefix'].upper()
    if mandatory_prefix in ['NONE', 'IGNORE']:
        return 'ZYDIS_MANDATORY_PREFIX_NONE'
    return 'ZYDIS_MANDATORY_PREFIX_' + mandatory_prefix


def get_vector_length(insn):
    vector_length = zydis_vector_length(insn.get('filters', {}).get('vector_length', 'placeholder'))
    evex_vector_length = zydis_vector_length(insn.get('evex', {}).get('vector_length', 'default'))
    if vector_length != evex_vector_length:
        if vector_length == 'ZYDIS_VECTOR_LENGTH_INVALID':
            return evex_vector_length
        elif evex_vector_length == 'ZYDIS_VECTOR_LENGTH_INVALID':
            return vector_length
        raise ValueError('%s != %s for %s' % (vector_length, evex_vector_length, get_full_instruction(insn)))
    return vector_length


def get_size_hint(insn):
    flags = insn.get('flags', [])
    is_branching = 'short_branch' in flags or 'near_branch' in flags or 'far_branch' in flags
    visible_asz_scale = False
    visible_osz_scale = False
    hidden_asz_scale = False
    hidden_osz_scale = False
    has_rel = False
    for op in get_operands(insn, True):
        visible = op.get('visible', True)
        register = op.get('register', 'none')
        mem_base = op.get('mem_base', 'none')
        scale_factor = op.get('scale_factor', 'static')
        operand_type = op.get('operand_type', 'unused')
        if operand_type == 'rel':
            has_rel = True
        if register.startswith('asz') or scale_factor == 'asz' or operand_type == 'gpr_asz' or \
                mem_base not in ['none', 'sp', 'bp']:
            if visible:
                visible_asz_scale = True
            else:
                hidden_asz_scale = True
        if register.startswith('osz') or operand_type in ['gpr16_32_64', 'gpr32_32_64', 'gpr16_32_32'] or \
           (scale_factor == 'osz' and operand_type != 'imm'):
            if visible:
                visible_osz_scale = True
            else:
                hidden_osz_scale = True
    filters = insn.get('filters', {})
    if not visible_asz_scale and hidden_asz_scale and 'address_size' not in filters:
        return 'ZYDIS_SIZE_HINT_ASZ'
    if not visible_osz_scale and hidden_osz_scale and 'operand_size' not in filters and not is_branching:
        return 'ZYDIS_SIZE_HINT_OSZ'
    if has_rel and not is_branching:
        return 'ZYDIS_SIZE_HINT_OSZ'
    if insn.get('forced_hint', False):
        return 'ZYDIS_SIZE_HINT_OSZ'
    return 'ZYDIS_SIZE_HINT_NONE'


def get_operand_mask(insn):
    operands = get_operands(insn, False)
    if len(operands) > 5:
        raise InvalidInstructionException('Max allowed operand count is 5')
    op_mask = len(operands)
    bit_offset = 3
    for op in operands:
        op_type = op['operand_type']
        if op_type in ['implicit_reg', 'gpr8', 'gpr16', 'gpr32', 'gpr64', 'gpr16_32_64', 'gpr32_32_64', 'gpr16_32_32', 'gpr_asz',
                       'fpr', 'mmx', 'xmm', 'ymm', 'zmm', 'tmm', 'bnd', 'sreg', 'cr', 'dr', 'mask']:
            op_type_value = 0
        elif op_type in ['implicit_mem', 'mem', 'mem_vsibx', 'mem_vsiby', 'mem_vsibz', 'agen', 'agen_norel', 'mib', 'moffs']:
            op_type_value = 1
        elif op_type == 'ptr':
            op_type_value = 2
        elif op_type in ['implicit_imm1', 'imm', 'rel',]:
            op_type_value = 3
        else:
            raise InvalidInstructionException('Invalid operand_type: ' + op_type)
        op_mask |= op_type_value << bit_offset
        bit_offset += 2

    return op_mask


def generate_encoder_lookup_table(instructions):
    lookup_entries = []
    last_mnemonic = 'invalid'
    last_entry = [0, 0]
    for i, insn in enumerate(instructions):
        if insn['mnemonic'] != last_mnemonic:
            lookup_entries.append(last_entry)
            last_mnemonic = insn['mnemonic']
            last_entry = [i, 0]
        last_entry[1] += 1
    lookup_entries.append(last_entry)

    table = 'const ZydisEncoderLookupEntry encoder_instruction_lookup[] =\n{\n'
    for index, count in lookup_entries:
        table += '    { 0x%04X, %d },\n' % (index, count)
    table += '};\n'
    return table


def generate_encoder_table(instructions):
    table = 'const ZydisEncodableInstruction encoder_instructions[] =\n{\n'
    for insn in instructions:
        table += '    { '
        table += '0x%04X, ' % insn['instruction_table_index']
        table += '0x%04X, ' % get_operand_mask(insn)
        table += '0x%s, ' % insn['opcode']
        table += '0x%02X, ' % get_modrm(insn)
        table += 'ZYDIS_INSTRUCTION_ENCODING_%s, ' % zydis_instruction_encoding(insn.get('encoding', 'default'))
        table += 'ZYDIS_OPCODE_MAP_%s, ' % insn.get('opcode_map', 'default').upper()
        table += '%s, ' % get_width_flags(insn['allowed_modes'])
        table += '%s, ' % get_width_flags(get_effective_address_size(insn))
        table += '%s, ' % get_width_flags(insn['operand_size'])
        table += '%s, ' % get_mandatory_prefix(insn)
        table += '%s, ' % zydis_bool(insn.get('filters', {}).get('rex_w', 'placeholder') == '1')
        table += '%s, ' % get_vector_length(insn)
        table += '%s ' % get_size_hint(insn)
        table += '},\n'
    table += '};\n'
    return table


def get_filters(insn):
    filters = insn.get('filters', {})
    important_filters = (
        filters.get('mode', 'placeholder'),
        filters.get('rex_w', 'placeholder'),
        filters.get('operand_size', 'placeholder'),
        filters.get('address_size', 'placeholder')
    )
    return 'mode=%s,rex_w=%s,operand_size=%s,address_size=%s' % important_filters


def get_osz(insn):
    return insn.get('filters', {}).get('operand_size', 'placeholder').replace('!', '')


def get_basic_encoding(insn):
    encoding = {}
    dict_append(encoding, insn, 'operands')
    dict_append(encoding, insn, 'opsize_map')
    dict_append(encoding, insn, 'adsize_map')
    dict_append(encoding, insn, 'cpl')
    dict_append(encoding, insn, 'flags')
    dict_append(encoding, insn, 'meta_info')
    dict_append(encoding, insn, 'prefix_flags')
    dict_append(encoding, insn, 'exception_class')
    dict_append(encoding, insn, 'affected_flags')
    dict_append(encoding, insn, 'vex')
    dict_append(encoding, insn, 'evex')
    dict_append(encoding, insn, 'mvex')
    dict_append(encoding, insn, 'comment')
    return encoding


def merge_instruction_features(dest, src):
    dest['allowed_modes'] |= src['allowed_modes']
    if dest['address_size'] != src['address_size']:
        raise ValueError('Invalid duplicate (address size): ' + get_full_instruction(src))
    if dest['operand_size'] != src['operand_size']:
        if dest['operand_size_no_concat'] or src['operand_size_no_concat']:
            if get_osz(dest) != get_osz(src):
                raise ValueError('Invalid duplicate (operand size): ' + get_full_instruction(src))
            dest['forced_hint'] = True
        dest['operand_size'] |= src['operand_size']


class InstructionManipulator(ABC):

    def __init__(self, instructions):
        self.insn_db = instructions

    @abstractmethod
    def get_encoding(self, insn):
        pass

    @abstractmethod
    def update(self, insn1, insn2):
        pass

    def transform(self):
        last_mnemonic = ''
        encoding_list = []
        insn_list = []
        for insn in self.insn_db:
            mnemonic = insn['mnemonic']
            if mnemonic != last_mnemonic:
                last_mnemonic = mnemonic
                encoding_list = []
                insn_list = []
            encoding = self.get_encoding(insn)
            if encoding not in encoding_list:
                encoding_list.append(encoding)
                insn_list.append(insn)
                continue
            self.update(insn_list[encoding_list.index(encoding)], insn)


class PrefixEliminator(InstructionManipulator):

    def get_encoding(self, insn):
        encoding = copy.deepcopy(get_basic_encoding(insn))
        if 'operands' in encoding:
            del encoding['operands']
            encoding['operands'] = copy.deepcopy(get_operands(insn))
        if 'comment' in encoding:
            del encoding['comment']
        if 'affected_flags' in encoding:
            del encoding['affected_flags']
        if 'filters' in insn:
            filters = copy.deepcopy(insn['filters'])
            encoding['filters'] = filters
            if 'operand_size' in filters:
                del filters['operand_size']
            mandatory_prefix = filters.get('mandatory_prefix', 'none').upper()
            if 'mandatory_prefix' in filters and mandatory_prefix in ['NONE', 'IGNORE']:
                del filters['mandatory_prefix']
            prefix_flags = encoding.get('prefix_flags', [])
            if mandatory_prefix == 'F2' and 'accepts_repnerepnz' in prefix_flags:
                del filters['mandatory_prefix']
            if mandatory_prefix == 'F3' and ('accepts_rep' in prefix_flags or 'accepts_reperepz' in prefix_flags):
                del filters['mandatory_prefix']
        return encoding

    def update(self, insn1, insn2):
        merge_instruction_features(insn1, insn2)
        insn2['redundant'] = True


class ForceHiddenOperandSizes(InstructionManipulator):

    def get_encoding(self, insn):
        encoding = copy.deepcopy(get_basic_encoding(insn))
        if 'operands' in encoding:
            for op in encoding['operands']:
                if op['operand_type'] == 'implicit_reg' and not op.get('visible', True) and op['register'][0] in ['r', 'e']:
                    op['register'] = op['register'][1:]
        if 'comment' in encoding:
            del encoding['comment']
        if 'affected_flags' in encoding:
            del encoding['affected_flags']
        if 'filters' in insn:
            filters = copy.deepcopy(insn['filters'])
            encoding['filters'] = filters
            if 'rex_w' in filters:
                del filters['rex_w']
        return encoding

    def update(self, insn1, insn2):
        insn1['forced_hint'] = True
        insn2['redundant'] = True


class MergabilityDetector(InstructionManipulator):

    def get_encoding(self, insn):
        encoding = copy.deepcopy(get_basic_encoding(insn))
        if 'operands' in encoding:
            for op in encoding['operands']:
                if op['operand_type'] in ['gpr16', 'gpr32', 'gpr64', 'gpr32_32_64', 'gpr16_32_32']:
                    if 'element_type' in op:
                        del op['element_type']
                    if 'width16' in op:
                        del op['width16']
                    if 'width32' in op:
                        del op['width32']
                    if 'width64' in op:
                        del op['width64']
                    op['operand_type'] = 'mergable'
        if 'filters' in insn:
            filters = copy.deepcopy(insn['filters'])
            encoding['filters'] = filters
            if 'rex_w' in filters:
                del filters['rex_w']
            if 'mode' in filters:
                del filters['mode']
        if 'comment' in encoding:
            del encoding['comment']
        return encoding

    def update(self, insn1, insn2):
        insn2['mergable'] = True


class Is4Detector(InstructionManipulator):

    def get_encoding(self, insn):
        is4_detected = False
        encoding = copy.deepcopy(get_basic_encoding(insn))
        if 'operands' in encoding:
            for op in encoding['operands']:
                is4_detected |= op.get('encoding', '') == 'is4'
            if is4_detected:
                for op in encoding['operands']:
                    if op.get('encoding', '') not in ['is4', 'modrm_rm']:
                        continue
                    del op['encoding']
                    del op['element_type']
                    del op['width16']
                    del op['width32']
                    del op['width64']
        if is4_detected and 'comment' in encoding:
            del encoding['comment']
        if insn['mnemonic'] == 'vfmaddpd':
            print('>>>' + str(encoding))
        return encoding

    def update(self, insn1, insn2):
        insn1['is4_swappable'] = True
        insn2['is4_swappable'] = True


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description='Generates tables needed for encoder')
    parser.add_argument('--mode', choices=['generate-tables', 'instruction-order-test', 'stats', 'print', 'print-all', 'encodings', 'filters'], default='generate-tables')
    args = parser.parse_args()

    with open('..\\Data\\instructions.json', 'r') as f:
        db = json.load(f)

    max_args = 0
    max_visible_args = 0
    unique_instructions = []
    unique_encodings = {}
    instructions_by_encoding = {}
    operand_types = {}
    operand_encodings = {}
    unique_filters = set()
    for insn in db:
        unique_filters.add(get_filters(insn))
        insn['redundant'] = False
        insn['allowed_modes'] = ZydisWidthFlag.w_invalid
        try:
            insn['allowed_modes'] = get_instruction_mode(insn)
        except InvalidInstructionException:
            print('Invalid: ' + get_full_instruction(insn))
        insn['address_size'] = get_address_size(insn)
        insn['operand_size'] = get_operand_size(insn)
        insn['operand_size_no_concat'] = insn.get('filters', {}).get('operand_size', 'placeholder')[0] == '!'

        # Only unique signatures allowed
        # This logic MUST behave exactly as in Zydis generator to correctly reference instruction definitions from encoder's tables
        # instruction-order-test generates test tables which can be used to verify this critical assumption with C test tool
        mnemonic = insn['mnemonic']
        if mnemonic not in unique_encodings:
            unique_encodings[mnemonic] = { 'encodings': [], 'indexes': [] }
        current_encoding = get_basic_encoding(insn)
        if current_encoding in unique_encodings[mnemonic]['encodings']:
            current_encoding_index = unique_encodings[mnemonic]['encodings'].index(current_encoding)
            instruction_index = unique_encodings[mnemonic]['indexes'][current_encoding_index]
            merge_instruction_features(unique_instructions[instruction_index], insn)
            continue
        unique_encodings[mnemonic]['encodings'].append(current_encoding)
        unique_encodings[mnemonic]['indexes'].append(len(unique_instructions))

        # Unique instruction list and reverse lookup is required for encoder table
        # Encoding-based lookup is needed for instruction order test tables
        encoding = insn.get('encoding', 'default')
        if encoding not in instructions_by_encoding:
            instructions_by_encoding[encoding] = []
            operand_types[encoding] = set()
        insn['instruction_table_index'] = len(instructions_by_encoding[encoding])
        insn['encodable'] = True
        filters = insn.get('filters', {})
        if filters.get('feature_amd', '0') == '1':
            insn['encodable'] = False
        if mnemonic == 'nop':
            for feature in ['mpx', 'cldemote', 'cet', 'knc']:
                if filters.get('feature_' + feature, '1') == '0':
                    insn['encodable'] = False
                    break
        instructions_by_encoding[encoding].append(len(unique_instructions))
        unique_instructions.append(insn)

        # Track max operand count for stats mode
        operands = get_operands(insn)
        if len(operands) > max_visible_args:
            max_visible_args = len(operands)
        all_operands = get_operands(insn, True)
        if len(all_operands) > max_args:
            max_args = len(all_operands)

        # Track unique operand types for stats mode
        for op in operands:
            operand_types[encoding].add(get_full_operand(op))
            op_type = op['operand_type']
            if op_type not in operand_encodings:
                operand_encodings[op_type] = set()
            operand_encodings[op_type].add(op.get('encoding', 'none'))

    #MergabilityDetector(filter(lambda insn: insn['encodable'], unique_instructions)).transform()
    #Is4Detector(filter(lambda insn: insn['encodable'], unique_instructions)).transform()
    PrefixEliminator(filter(lambda insn: insn['encodable'], unique_instructions)).transform()
    ForceHiddenOperandSizes(filter(lambda insn: insn['encodable'] and not insn['redundant'], unique_instructions)).transform()
    if args.mode == 'generate-tables':
        def is_encodable(insn):
            if not insn['encodable']:
                return False
            if insn['redundant']:
                return False
            if insn['allowed_modes'] == ZydisWidthFlag.w_invalid:
                # print('Rejecting ignored instruction: ' + get_full_instruction(insn), file=sys.stderr)
                return False
            return True

        encodable_instructions = list(filter(is_encodable, unique_instructions))
        print(generate_encoder_lookup_table(encodable_instructions))
        print(generate_encoder_table(encodable_instructions))
    elif args.mode == 'instruction-order-test':
        for encoding, lookup in sorted(instructions_by_encoding.items()):
            print(generate_instruction_order_test_table(zydis_instruction_encoding(encoding), get_instructions(unique_instructions, lookup)))
    elif args.mode == 'stats':
        print('max_args=%d' % max_args)
        print('max_visible_args=%d' % max_visible_args)
        print('unique_instructions=%d' % len(unique_instructions))
        for encoding, lookup in sorted(instructions_by_encoding.items()):
            print('%s_instructions=%d' % (encoding, len(lookup)))
        for encoding, op_types in sorted(operand_types.items()):
            print('Operand types for encoding %s:' % encoding)
            for op_type in sorted(op_types):
                print('    ' + op_type)
    elif args.mode == 'encodings':
        for op_type, encodings in sorted(operand_encodings.items()):
            print('%s:' % op_type)
            for encoding in sorted(encodings):
                print('    ' + encoding)
    elif args.mode == 'filters':
        for filter in sorted(unique_filters):
            print(filter)
    else:
        for insn in unique_instructions:
            encoding = insn.get('encoding', 'default')
            prefix = encoding.upper() + ':' if encoding != 'default' else ''
            hint = get_size_hint(insn)
            scaling = ''
            if hint == 'ZYDIS_SIZE_HINT_ASZ':
                scaling = 'HASZ.'
            elif hint == 'ZYDIS_SIZE_HINT_OSZ':
                scaling = 'HOSZ.'
            redundant = '' if not insn['redundant'] else 'REDUNDANT.'
            mergable = '' if not insn.get('mergable', False) else 'MERGABLE.'
            non_encodable = '' if insn['encodable'] else 'NE.'
            is4_swappable = '' if not insn.get('is4_swappable', False) else 'IS4S.'
            print(is4_swappable + non_encodable + mergable + redundant + scaling + prefix + get_full_instruction(insn, args.mode == 'print-all'))
