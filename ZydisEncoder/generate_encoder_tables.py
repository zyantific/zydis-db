#!/usr/bin/env python3
import json
import argparse
from instruction_manipulators import *


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


def get_estimated_size(insn):
    minimal_len = 1
    filters = insn.get('filters', {})
    rex_w = filters.get('rex_w', 'placeholder')
    encoding = insn.get('encoding', 'default')
    opcode_map = insn.get('opcode_map', 'default')
    if encoding not in ['xop', 'vex', 'evex', 'mvex'] and get_mandatory_prefix(insn) != 'ZYDIS_MANDATORY_PREFIX_NONE':
        minimal_len += 1
    if encoding in ['default', '3dnow']:
        if opcode_map == 'default':
            pass
        elif opcode_map == '0f':
            minimal_len += 1
        elif opcode_map in ['0f0f', '0f38', '0f3a']:
            minimal_len += 2
        else:
            raise InvalidInstructionException('Invalid opcode map: ' + opcode_map)
        if rex_w == '1':
            minimal_len += 1
    elif encoding == 'xop':
        minimal_len += 3
    elif encoding == 'vex':
        minimal_len += 2
        if rex_w == '1' or opcode_map != '0f':
            minimal_len += 1
    elif encoding in ['evex', 'mvex']:
        minimal_len += 4
    else:
        raise InvalidInstructionException('Invalid encoding: ' + encoding)

    overhead = 0
    has_sib = False
    has_mem = False
    has_modrm = False
    for op in get_operands(insn, False):
        op_encoding = op.get('encoding', 'none')
        if op_encoding in ['modrm_reg', 'modrm_rm']:
            has_modrm = True
            """
            elif op_encoding == 'disp16_32_64':
                minimal_len += 2
                overhead += 2
            """
        elif op_encoding in ['uimm8', 'simm8', 'jimm8']:
            minimal_len += 1
        elif op_encoding in ['uimm16', 'simm16', 'jimm16', 'uimm16_32_64', 'uimm16_32_32', 'simm16_32_64', 'simm16_32_32', 'jimm16_32_64', 'jimm16_32_32']:
            minimal_len += 2
            if op_encoding in ['uimm16_32_64', 'simm16_32_64', 'jimm16_32_64']:
                overhead += 6
            elif op_encoding in ['uimm16_32_32', 'simm16_32_32', 'jimm16_32_32']:
                overhead += 2
        elif op_encoding in ['uimm32', 'simm32', 'jimm32', 'uimm32_32_64', 'simm32_32_64', 'jimm32_32_64']:
            minimal_len += 4
            if op_encoding in ['uimm32_32_64', 'simm32_32_64', 'jimm32_32_64']:
                minimal_len += 4
        elif op_encoding in ['uimm64', 'simm64', 'jimm64']:
            minimal_len += 8

        op_type = op['operand_type']
        if op_type in ['implicit_mem', 'mem', 'agen', 'agen_norel']:
            has_mem = True
        elif op_type in ['mem_vsibx', 'mem_vsiby', 'mem_vsibz', 'mib']:
            has_mem = True
            has_sib = True
    if has_modrm or get_modrm(insn):
        minimal_len += 1
    if has_sib or (has_mem and int(filters.get('modrm_rm', '0')) == 4):
        minimal_len += 1

    return minimal_len, minimal_len + overhead


def is_swappable(insn):
    if 'swappable' not in insn:
        return False
    return (insn['swappable'] & 1) != 0


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
        members = [
            '0x%04X' % insn['instruction_table_index'],
            '0x%04X' % get_operand_mask(insn),
            '0x%s' % insn['opcode'],
            '0x%02X' % get_modrm(insn),
            'ZYDIS_INSTRUCTION_ENCODING_%s' % zydis_instruction_encoding(insn.get('encoding', 'default')),
            'ZYDIS_OPCODE_MAP_%s' % insn.get('opcode_map', 'default').upper(),
            get_width_flags(insn['allowed_modes']),
            get_width_flags(get_effective_address_size(insn)),
            get_width_flags(insn['operand_size']),
            get_mandatory_prefix(insn),
            zydis_bool(insn.get('filters', {}).get('rex_w', 'placeholder') == '1'),
            get_vector_length(insn),
            get_size_hint(insn),
            zydis_bool(is_swappable(insn)),
        ]
        table += '    { %s },\n' % ', '.join(members)
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


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description='Generates tables needed for encoder')
    parser.add_argument('--mode', choices=['generate-tables', 'stats', 'print', 'print-all', 'encodings', 'filters'], default='generate-tables')
    args = parser.parse_args()

    with open('..\\Data\\instructions.json', 'r') as f:
        db = json.load(f)

    # Gathering instructions
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
        insn['min_size'], insn['max_size'] = get_estimated_size(insn)

        # Only unique signatures allowed
        # This logic MUST behave exactly as in Zydis generator to correctly reference instruction definitions from encoder's tables
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

        # Unique instruction list and reverse lookup is required for encoder tables
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

    # Post-processing
    get_unique_instructions = lambda insn: insn['encodable'] and not insn['redundant']
    #MergabilityDetector(filter(lambda insn: insn['encodable'], unique_instructions)).transform()
    #Is4Detector(filter(lambda insn: insn['encodable'], unique_instructions)).transform()
    PrefixEliminator(filter(get_unique_instructions, unique_instructions)).transform()
    ForceHiddenOperandSizes(filter(get_unique_instructions, unique_instructions)).transform()
    SwappableOperandDetector(filter(get_unique_instructions, unique_instructions)).transform()
    for i, encoding_info in enumerate(unique_encodings['mov']['encodings']):
        mov_insn = unique_instructions[unique_encodings['mov']['indexes'][i]]
        dest = mov_insn['operands'][0]
        src = mov_insn['operands'][1]
        if dest['operand_type'] == 'gpr16_32_64' and dest['encoding'] == 'opcode' and \
           src['operand_type'] == 'imm' and src['encoding'] == 'uimm16_32_64':
            mov_insn['swappable'] = True
            break

    def instrucion_sorter(insn):
        flags = insn.get('flags', [])
        branch_type = 0
        if 'short_branch' in flags:
            branch_type = 1
        elif 'near_branch' in flags:
            branch_type = 2
        elif 'far_branch' in flags:
            branch_type = 3
        return (insn['mnemonic'],
                insn['redundant'] or not insn['encodable'],
                insn['min_size'],
                insn['max_size'],
                insn.get('swappable', 0),
                branch_type)
    unique_instructions.sort(key=instrucion_sorter)

    # Execute requested actions
    if args.mode == 'generate-tables':
        def is_encodable(insn):
            if not insn['encodable']:
                return False
            if insn['redundant']:
                return False
            if insn['allowed_modes'] == ZydisWidthFlag.w_invalid:
                return False
            return True

        encodable_instructions = list(filter(is_encodable, unique_instructions))
        print(generate_encoder_lookup_table(encodable_instructions))
        print(generate_encoder_table(encodable_instructions))
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
            prefix = encoding.upper() + '.' if encoding != 'default' else ''
            hint = get_size_hint(insn)
            scaling = ''
            if hint == 'ZYDIS_SIZE_HINT_ASZ':
                scaling = 'HASZ.'
            elif hint == 'ZYDIS_SIZE_HINT_OSZ':
                scaling = 'HOSZ.'
            decorators = [
                'MIN%02d.' % insn['min_size'],
                'MAX%02d.' % insn['max_size'],
                '' if insn['encodable'] else 'NE.',
                '' if not insn.get('is4_swappable', False) else 'IS4S.',
                '' if not insn.get('mergable', False) else 'MERGABLE.',
                '' if not insn.get('redundant', False) else 'REDUNDANT.',
                '' if not insn.get('swappable', False) else 'SWAPPABLE%d.' % insn['swappable'],
                scaling,
                prefix,
                insn['opcode'].upper() + '.',
            ]
            print(''.join(decorators) + get_full_instruction(insn, args.mode == 'print-all'))
