#!/usr/bin/env python3
import json
import argparse
from instruction_manipulators import *
from collections import Counter, defaultdict
from pathlib import Path


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
        elif op_type in ['implicit_imm1', 'imm', 'rel', 'abs']:
            op_type_value = 3
        else:
            raise InvalidInstructionException('Invalid operand_type: ' + op_type)
        op_mask |= op_type_value << bit_offset
        bit_offset += 2

    return op_mask


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
    table += '};\n\n'
    return table


def generate_encoder_table(instructions):
    table = 'const ZydisEncodableInstruction encoder_instructions[] =\n{\n'
    for insn in instructions:
        filters = insn.get('filters', {})
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
            zydis_bool(filters.get('rex_w', 'placeholder') == '1'),
            zydis_bool(insn['rex2'] == ZydisRex2.rex2),
            zydis_bool(filters.get('evex_nd', 'placeholder') == '1'),
            zydis_bool(filters.get('evex_nf', 'placeholder') == '1'),
            zydis_bool('apx_osz' in insn),
            get_vector_length(insn),
            get_size_hint(insn),
            zydis_bool(is_swappable(insn)),
        ]
        table += '    { %s },\n' % ', '.join(members)
    table += '};\n'
    return table


def generate_rel_info(instructions):
    rel_instructions = {}
    for insn in instructions:
        has_rel = False
        has_imm = False
        for op in get_operands(insn):
            if op['operand_type'] == 'imm':
                has_imm = True
            if op['operand_type'] != 'rel':
                continue
            if has_rel:
                raise InvalidInstructionException()
            has_rel = True
            mnemonic = insn['mnemonic']
            prefix_flags = insn.get('prefix_flags', [])
            accepts_bound = 'accepts_bound' in prefix_flags
            if mnemonic not in rel_instructions:
                rel_instructions[mnemonic] = {
                    'size': [[0, 0, 0] for _ in range(3)],
                    'scaling_hints': get_size_hint(insn),
                    'branch_hits': zydis_bool('accepts_branch_hints' in prefix_flags),
                    'bound': zydis_bool(accepts_bound)
                }
            if accepts_bound:
                rel_instructions[mnemonic]['bound'] = zydis_bool(accepts_bound)
            encoding = op['encoding']
            mode_filter = insn.get('filters', {}).get('mode', 'all')
            if mode_filter == '64':
                modes = [2]
            elif mode_filter == '!64':
                modes = [0, 1]
            elif mode_filter == 'all':
                modes = [0, 1, 2]
            else:
                raise InvalidInstructionException()
            scaling_type = 'osz'
            address_size = insn.get('filters', {}).get('address_size', '0')
            if address_size != '0':
                scaling_type = 'asz'
                address_size = int(address_size) >> 5
            for mode in modes:
                if encoding == 'jimm8':
                    size = insn['min_size']
                    if scaling_type == 'asz' and address_size != mode:
                        size += 1
                    rel_instructions[mnemonic]['size'][mode][0] = size
                elif encoding == 'jimm32':
                    size = insn['min_size']
                    if scaling_type == 'osz' and mode == 0:
                        size += 1
                    rel_instructions[mnemonic]['size'][mode][2] = size
                elif encoding == 'jimm16_32_32':
                    size = insn['min_size']
                    if scaling_type == 'osz' and mode != 0:
                        size += 1
                    rel_instructions[mnemonic]['size'][mode][1] = size
                    size = insn['max_size']
                    if scaling_type == 'osz' and mode == 0:
                        size += 1
                    rel_instructions[mnemonic]['size'][mode][2] = size
                else:
                    raise InvalidInstructionException()
        if has_rel and has_imm:
            raise InvalidInstructionException()

    rel_info = []
    rel_mnemonics = []
    for mnemonic, info in rel_instructions.items():
        try:
            rel_mnemonics[rel_info.index(info)].append(mnemonic)
        except ValueError:
            rel_info.append(info)
            rel_mnemonics.append([mnemonic])

    func = 'const ZydisEncoderRelInfo *ZydisGetRelInfo(ZydisMnemonic mnemonic)\n{\n    static const ZydisEncoderRelInfo info_lookup[%d] =\n    {\n' % len(rel_info)
    for info in rel_info:
        lookup = ''
        for i in range(3):
            lookup += '{ %d, %d, %d }, ' % (info['size'][i][0], info['size'][i][1], info['size'][i][2])
        func += '        { { %s }, %s, %s, %s },\n' % (lookup[:-2], info['scaling_hints'], info['branch_hits'], info['bound'])
    func += '    };\n\n    switch (mnemonic)\n    {\n'
    for i, case in enumerate(rel_mnemonics):
        for mnemonic in case:
            func += '    case ZYDIS_MNEMONIC_%s:\n' % mnemonic.upper()
        func += '        return &info_lookup[%d];\n' % i
    func += '    default:\n        return ZYAN_NULL;\n    }\n}\n'
    return func


def generate_cc(instructions):
    cc_instructions = []
    scc_values = (
        'ZYDIS_SCC_NONE',
        'ZYDIS_SCC_O',
        'ZYDIS_SCC_NO',
        'ZYDIS_SCC_B',
        'ZYDIS_SCC_NB',
        'ZYDIS_SCC_Z',
        'ZYDIS_SCC_NZ',
        'ZYDIS_SCC_BE',
        'ZYDIS_SCC_NBE',
        'ZYDIS_SCC_S',
        'ZYDIS_SCC_NS',
        'ZYDIS_SCC_TRUE',
        'ZYDIS_SCC_FALSE',
        'ZYDIS_SCC_L',
        'ZYDIS_SCC_NL',
        'ZYDIS_SCC_LE',
        'ZYDIS_SCC_NLE',
    )
    for insn in instructions:
        if not insn['apx_cc'] and not insn['apx_scc']:
            continue
        filters = insn.get('filters', {})
        info = (insn['mnemonic'], scc_values[1 + int(filters['evex_scc']) if insn['apx_scc'] else 0])
        if not cc_instructions or cc_instructions[-1] != info:
            cc_instructions.append(info)

    func = 'ZyanBool ZydisGetCcInfo(ZydisMnemonic mnemonic, ZydisSourceConditionCode *scc)\n{\n    switch (mnemonic)\n    {\n'
    cases = defaultdict(list)
    for mnemonic, scc in cc_instructions:
        cases[scc].append(mnemonic)
    for scc, mnemonics in cases.items():
        for mnemonic in mnemonics:
            func += '    case ZYDIS_MNEMONIC_%s:\n' % mnemonic.upper()
        func += '        *scc = %s;\n        return ZYAN_TRUE;\n' % scc
    func += '    default:\n        *scc = ZYDIS_SCC_NONE;\n        return ZYAN_FALSE;\n    }\n}\n'
    return func


def get_filters(insn):
    filters = insn.get('filters', {})
    important_filters = (
        filters.get('mode', 'placeholder'),
        filters.get('rex_w', 'placeholder'),
        filters.get('operand_size', 'placeholder'),
        filters.get('address_size', 'placeholder')
    )
    return 'mode=%s,rex_w=%s,operand_size=%s,address_size=%s' % important_filters


def get_definition(insn, allow_invisible=False):
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
    return ''.join(decorators) + get_full_instruction(insn, allow_invisible)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description='Generates tables needed for encoder')
    parser.add_argument('--mode', choices=['generate', 'generate-tables', 'generate-rel-info', 'generate-cc', 'stats', 'print', 'print-all', 'encodings', 'filters', 'meta'], default='generate-tables')
    parser.add_argument('--outdir', default='')
    args = parser.parse_args()

    with open('../Data/instructions.json', 'r') as f:
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
        insn['rex2'] = get_rex2(insn)
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
    manipulators = [
        PrefixEliminator,
        ForceHiddenOperandSizes,
        SwappableOperandDetector,
        CcDetector,
        ApxScalingDetector,
    ]
    for manipulator in manipulators:
        manipulator(filter(get_unique_instructions, unique_instructions)).transform()
    for i, encoding_info in enumerate(unique_encodings['mov']['encodings']):
        mov_insn = unique_instructions[unique_encodings['mov']['indexes'][i]]
        dest = mov_insn['operands'][0]
        src = mov_insn['operands'][1]
        if dest['operand_type'] == 'gpr16_32_64' and dest['encoding'] == 'opcode' and \
           src['operand_type'] == 'imm' and src['encoding'] == 'simm16_32_64':
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
    if args.mode in ['generate', 'generate-tables', 'generate-rel-info', 'generate-cc']:
        def is_encodable(insn):
            if not insn['encodable']:
                return False
            if insn['redundant']:
                return False
            if insn['allowed_modes'] == ZydisWidthFlag.w_invalid:
                return False
            return True

        encodable_instructions = list(filter(is_encodable, unique_instructions))
        gen_tables = args.mode == 'generate-tables'
        gen_rel_info = args.mode == 'generate-rel-info'
        gen_cc = args.mode == 'generate-cc'
        if args.mode == 'generate':
            gen_tables = gen_rel_info = gen_cc = True

        tables = rel_info = cc = None
        if gen_tables:
            tables = generate_encoder_lookup_table(encodable_instructions)
            tables += generate_encoder_table(encodable_instructions)
        if gen_rel_info:
            rel_info = generate_rel_info(encodable_instructions)
        if gen_cc:
            cc = generate_cc(encodable_instructions)

        def print_stdout(info, filename):
            print(info)

        def print_file(info, filename):
            with open(Path(args.outdir) / filename, 'w', newline='') as f:
                f.write(info)

        output_function = print_file if args.mode == 'generate' else print_stdout
        if tables is not None:
            output_function(tables, 'EncoderTables.inc')
        if rel_info is not None:
            output_function(rel_info, 'GetRelInfo.inc')
        if cc is not None:
            output_function(cc, 'GetCcInfo.inc')
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
    elif args.mode == 'meta':
        meta = Counter()
        for insn in unique_instructions:
            meta[str(insn['meta_info'])] += 1
        for t in meta.items():
            print('%s: %d' % t)
    else:
        for insn in unique_instructions:
            filters = insn.get('filters', {})
            condition = True
            # All APX EEVEX instructions
            # condition = insn['meta_info']['extension'].startswith('APX') and insn.get('encoding', '') == 'evex'
            # Extra conditions for map4 (promoted legacy space)
            # condition &= any([op['operand_type'] == 'mem' for op in get_operands(insn)])
            # condition &= insn.get('opcode_map', '') == 'map4'
            # All APX instructions without evex_(nd|nf) filters
            # condition = 'evex_nf' not in filters and 'evex_nd' not in filters and insn['meta_info']['extension'].startswith('APX')
            # All APX CFCMOVcc and CMOVcc
            # condition = (insn['mnemonic'].startswith('cmov') or insn['mnemonic'].startswith('cfcmov')) and insn.get('encoding', '') == 'evex'
            # All APX CFCMOVcc and CMOVcc (2nd method)
            # condition = insn['opcode'].startswith('4') and insn.get('opcode_map', 'default') == 'map4' and 'evex_nf' in filters and 'evex_nd' in filters
            # All APX SCC instructions
            # condition = 'evex_scc' in filters
            # All APX with REX2 forbidden
            # condition = filters.get('rex_2', '') == 'no_rex2'
            if not condition:
                continue
            print(get_definition(insn, args.mode == 'print-all'))
