#!/usr/bin/env python3
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
    if dest['address_size'] != src['address_size']:
        raise ValueError('Invalid duplicate (address size): ' + get_full_instruction(src))
    if dest['operand_size'] != src['operand_size']:
        if dest['operand_size_no_concat'] or src['operand_size_no_concat']:
            if get_osz(dest) != get_osz(src):
                raise ValueError('Invalid duplicate (operand size): ' + get_full_instruction(src))
            dest['forced_hint'] = True
        dest['operand_size'] |= src['operand_size']
