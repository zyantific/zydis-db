#!/usr/bin/env python3
import copy
from abc import ABC, abstractmethod
from utils import *


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


"""
Manipulators used for table generation.
"""


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


class SwappableOperandDetector(InstructionManipulator):

    def __init__(self, instructions):
        super().__init__(instructions)
        self.db = {}

    def _get_modrm_operand_indexes(self, insn):
        op_reg = None
        op_rm = None
        for i, op in enumerate(insn['operands']):
            op_encoding = op.get('encoding', 'none')
            if op_encoding in ['modrm_rm', 'modrm_reg']:
                if op_encoding == 'modrm_rm':
                    op_rm = i
                else:
                    op_reg = i

        return op_reg, op_rm

    def get_encoding(self, insn):
        encoding = copy.deepcopy(get_basic_encoding(insn))
        if 'operands' in encoding and insn.get('encoding', 'default') == 'vex':
            op_reg, op_rm = self._get_modrm_operand_indexes(insn)
            operands = encoding['operands']
            if op_reg is not None and op_rm is not None and operands[op_reg]['operand_type'] == operands[op_rm]['operand_type']:
                operands[op_reg]['encoding'] = 'modrm_swappable'
                operands[op_rm]['encoding'] = 'modrm_swappable'
        if 'comment' in encoding:
            del encoding['comment']
        if 'filters' in insn:
            filters = copy.deepcopy(insn['filters'])
            if insn.get('encoding', 'default') == 'vex' and 'mandatory_prefix' in filters:
                del filters['mandatory_prefix']
            encoding['filters'] = filters
        return encoding

    def update(self, insn1, insn2):
        if self._get_modrm_operand_indexes(insn1)[0] > self._get_modrm_operand_indexes(insn2)[0]:
            insn1, insn2 = insn2, insn1
        mnemonic = insn1['mnemonic']
        if mnemonic not in self.db:
            self.db[mnemonic] = 1
        swappable_index = self.db[mnemonic]
        self.db[mnemonic] += 2
        insn1['swappable'] = swappable_index
        insn2['swappable'] = swappable_index + 1


"""
Manipulators used for testing, debugging, etc.
"""


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
        return encoding

    def update(self, insn1, insn2):
        insn1['is4_swappable'] = True
        insn2['is4_swappable'] = True
