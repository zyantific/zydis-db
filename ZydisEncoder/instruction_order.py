#!/usr/bin/env python3
from zydis_types import *
from utils import *


def generate_operand_definition(op):
    op_str = '{ '
    op_type = op['operand_type']
    if op_type != 'agen_norel':
        op_str += 'ZYDIS_SEMANTIC_OPTYPE_%s, ' % op_type.upper()
    else:
        op_str += 'ZYDIS_SEMANTIC_OPTYPE_AGEN, '
    visibility = 'EXPLICIT'
    if op_type in ['implicit_reg', 'implicit_mem']:
        if op.get('visible', True):
            visibility = 'IMPLICIT'
        else:
            visibility = 'HIDDEN'
    elif op_type == 'implicit_imm1':
        visibility = 'IMPLICIT'
    op_str += 'ZYDIS_OPERAND_VISIBILITY_%s, ' % visibility
    op_str += 'ZYDIS_OPERAND_ACTION_%s, ' % ZydisOperandAction[op.get('action', 'read')].value
    op_str += '{ %d, %d, %d }, ' % (op.get('width16', 0), op.get('width32', 0), op.get('width64', 0))
    op_str += 'ZYDIS_IELEMENT_TYPE_%s, ' % op.get('element_type', 'invalid').upper()
    if op_type == 'implicit_reg':
        register = ZydisRegister[op.get('register', 'none')]
        reg_class = ZydisRegisterClass.get_register_class(register)
        size_classes = [ZydisRegisterClass.regcGPROSZ, ZydisRegisterClass.regcGPRASZ, ZydisRegisterClass.regcGPRSSZ]
        size_regs = [ZydisRegister.asz_ip, ZydisRegister.ssz_ip, ZydisRegister.ssz_flags]
        if reg_class in size_classes or register in size_regs:
            implreg_type = 'STATIC'
            if reg_class is ZydisRegisterClass.regcGPROSZ:
                implreg_type = 'GPR_OSZ'
            elif reg_class is ZydisRegisterClass.regcGPRASZ:
                implreg_type = 'GPR_ASZ'
            elif reg_class is ZydisRegisterClass.regcGPRSSZ:
                implreg_type = 'GPR_SSZ'
            if register is ZydisRegister.asz_ip:
                implreg_type = 'IP_ASZ'
            elif register is ZydisRegister.ssz_ip:
                implreg_type = 'IP_SSZ'
            elif register is ZydisRegister.ssz_flags:
                implreg_type = 'FLAGS_SSZ'
            op_str += '{ .reg = { ZYDIS_IMPLREG_TYPE_%s, { .id = 0x%X } } }, ' % (
                implreg_type, ZydisRegisterClass.get_register_id(register) & 0x3F)
        else:
            op_str += '{ .reg = { ZYDIS_IMPLREG_TYPE_STATIC, { .reg = ZYDIS_REGISTER_%s } } }, ' % register.name.upper()
    elif op_type == 'implicit_mem':
        op_str += '{ .mem = { %d, ZYDIS_IMPLMEM_BASE_%s } }, ' % (
            ZydisSegment[op.get('mem_segment', 'none')].value, ZydisBaseRegister[op.get('mem_base', 'none')].value)
    else:
        op_str += '{ .encoding = ZYDIS_OPERAND_ENCODING_%s }, ' % op.get('encoding', 'none').upper()

    op_str += '%s, ' % zydis_bool(op.get('is_multisource4', False))
    op_str += '%s ' % zydis_bool(op.get('ignore_seg_override', False))
    op_str += '},\n'
    return op_str


def generate_instruction_entry(insn):
    all_operands = get_operands(insn, True)
    entry = '    {\n'
    entry += '        ZYDIS_MNEMONIC_%s, %d,\n' % (insn['mnemonic'].upper(), len(all_operands))
    if len(all_operands) == 0:
        entry += '    },\n'
        return entry
    entry += '        {\n'
    for op in all_operands:
        entry += '            ' + generate_operand_definition(op)
    entry += '        }\n'
    entry += '    },\n'
    return entry


def generate_instruction_order_test_table(table_name, instructions):
    table = 'const ZydisInstructionOrderTestItem ORDER_TEST_%s[] =\n{\n' % table_name
    for insn in instructions:
        table += generate_instruction_entry(insn)
    table += '};\n'
    return table
