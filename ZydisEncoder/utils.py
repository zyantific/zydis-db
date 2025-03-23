#!/usr/bin/env python3
from enum import IntFlag


def dict_append(dst_dict, src_dict, key):
    if key in src_dict:
        dst_dict[key] = src_dict[key]


def get_operands(insn, allow_invisible=False):
    if 'operands' not in insn:
        return []

    return [op for op in insn['operands'] if allow_invisible or op.get('visible', True)]


def serialize_dict(dict):
    return ','.join('%s=%s' % (k, str(v)) for k, v in sorted(dict.items()))


def get_full_operand(op):
    op_str = op['operand_type']
    op_clean = {k: v for k, v in op.items() if k not in ['operand_type', 'action']}
    if len(op_clean) > 0:
        op_str += '(%s)' % serialize_dict(op_clean)
    return op_str


def get_full_instruction(insn, allow_invisible=False):
    full_insn = insn['mnemonic']
    operands = [get_full_operand(op) for op in get_operands(insn, allow_invisible)]
    if len(operands) > 0:
        full_insn += ' ' + ', '.join(operands)
    if 'filters' in insn:
        filters = insn['filters']
        """
        if 'operand_size' in filters:
            filters['operand_size'] = 'removed'
        if 'address_size' in filters:
            filters['address_size'] = 'removed'
        """
        full_insn += ' [%s]' % serialize_dict(filters)
    if 'opsize_map' in insn:
        full_insn += ' opsize_map{%s}' % insn['opsize_map']
    if 'adsize_map' in insn:
        full_insn += ' adsize_map{%s}' % insn['adsize_map']
    if 'flags' in insn:
        full_insn += ' flags{%s}' % ','.join(insn['flags'])
    if 'evex' in insn:
        full_insn += ' evex{%s}' % serialize_dict(insn['evex'])
    if 'mvex' in insn:
        full_insn += ' mvex{%s}' % serialize_dict(insn['mvex'])

    return full_insn


def get_accepts_segment(insn):
    if 'remove_segment' in insn.get('prefix_flags', []):
        return False
    for op in get_operands(insn, True):
        if op['operand_type'] not in ['implicit_mem', 'mem', 'mem_vsibx', 'mem_vsiby', 'mem_vsibz', 'ptr', 'moffs', 'mib']:
            continue
        if op.get('ignore_seg_override', False):
            continue
        return True

    return False


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


def get_osz(insn):
    return insn.get('filters', {}).get('operand_size', 'placeholder').replace('!', '')


def merge_instruction_features(dest, src):
    dest['allowed_modes'] |= src['allowed_modes']
    dest['rex2'] |= src['rex2']
    if dest['address_size'] != src['address_size']:
        raise ValueError('Invalid duplicate (address size): ' + get_full_instruction(src))
    if dest['operand_size'] != src['operand_size']:
        if dest['operand_size_no_concat'] or src['operand_size_no_concat']:
            if get_osz(dest) != get_osz(src):
                raise ValueError('Invalid duplicate (operand size): ' + get_full_instruction(src))
            dest['forced_hint'] = True
        dest['operand_size'] |= src['operand_size']
    dest['min_size'], dest['max_size'] = get_estimated_size(dest)


def zydis_bool(val):
    return 'ZYAN_TRUE' if val else 'ZYAN_FALSE'


def zydis_instruction_encoding(encoding):
    encoding_name = encoding.upper()
    if encoding_name == 'DEFAULT':
        encoding_name = 'LEGACY'
    return encoding_name


def zydis_vector_length(length):
    if length in ['default', 'placeholder']:
        return 'ZYDIS_VECTOR_LENGTH_INVALID'
    elif length == '128':
        return 'ZYDIS_VECTOR_LENGTH_128'
    elif length == '256':
        return 'ZYDIS_VECTOR_LENGTH_256'
    elif length == '512':
        return 'ZYDIS_VECTOR_LENGTH_512'
    else:
        raise ValueError('Invalid vector length')


def get_rex2(insn):
    filters = insn.get('filters', {})
    return ZydisRex2[filters.get('rex_2', 'none')]


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
        if insn['rex2'] == ZydisRex2.rex2:
            minimal_len += 2
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


class InvalidInstructionException(Exception):
    pass


class ZydisWidthFlag(IntFlag):
    w_invalid = 0
    w_16 = 1
    w_32 = 2
    w_64 = 4


class ZydisRex2(IntFlag):
    none = 0
    no_rex2 = 1
    rex2 = 2
