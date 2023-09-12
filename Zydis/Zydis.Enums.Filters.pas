{***************************************************************************************************

  ZydisEditor

  Original Author : Florian Bernd

 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.

***************************************************************************************************}
unit Zydis.Enums.Filters;

interface

type
  TZYEnumInstructionEncoding = record
  public
    type Enum = (
      iencDEFAULT,
      ienc3DNOW,
      iencXOP,
      iencVEX,
      iencEVEX,
      iencMVEX
    );
  public
    const Names: array[Enum] of String = (
      'iencDEFAULT',
      'ienc3DNOW',
      'iencXOP',
      'iencVEX',
      'iencEVEX',
      'iencMVEX'
    );
  public
    const JSONStrings: array[Enum] of String = (
      'default',
      '3dnow',
      'xop',
      'vex',
      'evex',
      'mvex'
    );
  public
    const ZydisStrings: array[Enum] of String = (
      'DEFAULT',
      '3DNOW',
      'XOP',
      'VEX',
      'EVEX',
      'MVEX'
    );
  end;
  TZYInstructionEncoding = TZYEnumInstructionEncoding.Enum;

  TZYEnumOpcodeMap = record
  public
    type Enum = (
      omapDEFAULT,
      omap0F,
      omap0F0F,
      omap0F38,
      omap0F3A,
      omapMAP4,
      omapMAP5,
      omapMAP6,
      omapMAP7,
      omapXOP8,
      omapXOP9,
      omapXOPA
    );
  public
    const Names: array[Enum] of String = (
      'omapDEFAULT',
      'omap0F',
      'omap0F0F',
      'omap0F38',
      'omap0F3A',
      'omapMAP4',
      'omapMAP5',
      'omapMAP6',
      'omapMAP7',
      'omapXOP8',
      'omapXOP9',
      'omapXOPA'
    );
  public
    const JSONStrings: array[Enum] of String = (
      'default',
      '0f',
      '0f0f',
      '0f38',
      '0f3a',
      'map4',
      'map5',
      'map6',
      'map7',
      'xop8',
      'xop9',
      'xopa'
    );
  public
    const ZydisStrings: array[Enum] of String = (
      'DEFAULT',
      '0F',
      '0F0F',
      '0F38',
      '0F3A',
      'MAP4',
      'MAP5',
      'MAP6',
      'MAP7',
      'XOP8',
      'XOP9',
      'XOPA'
    );
  public
    const DisplayStrings: array[Enum] of String = (
      'DEFAULT',
      '0F',
      '0F0F',
      '0F38',
      '0F3A',
      'MAP4',
      'MAP5',
      'MAP6',
      'MAP7',
      'XOP8',
      'XOP9',
      'XOPA'
    );
  end;
  TZYOpcodeMap = TZYEnumOpcodeMap.Enum;

  TZYEnumFilterMode = record
  public
    type Enum = (
      modePlaceholder,
      mode16Bit,
      mode32Bit,
      mode64Bit,
      modeNot16Bit,
      modeNot32Bit,
      modeNot64Bit
    );
  public
    const Names: array[Enum] of String = (
      'modePlaceholder',
      'mode16Bit',
      'mode32Bit',
      'mode64Bit',
      'modeNot16Bit',
      'modeNot32Bit',
      'modeNot64Bit'
    );
  public
    const JSONStrings: array[Enum] of String = (
      'placeholder',
      '16',
      '32',
      '64',
      '!16',
      '!32',
      '!64'
    );
  end;
  TZYFilterMode = TZYEnumFilterMode.Enum;

  TZYEnumFilterModrmMod = record
  public
    type Enum = (
      mdPlaceholder,
      md0,
      md1,
      md2,
      md3,
      mdNot0,
      mdNot1,
      mdNot2,
      mdNot3
    );
  public
    const Names: array[Enum] of String = (
      'mdPlaceholder',
      'md0',
      'md1',
      'md2',
      'md3',
      'mdNot0',
      'mdNot1',
      'mdNot2',
      'mdNot3'
    );
  public
    const JSONStrings: array[Enum] of String = (
      'placeholder',
      '0',
      '1',
      '2',
      '3',
      '!0',
      '!1',
      '!2',
      '!3'
    );
  end;
  TZYFilterModrmMod = TZYEnumFilterModrmMod.Enum;

  TZYEnumFilterModrmReg = record
  public
    type Enum = (
      rgPlaceholder,
      rg0,
      rg1,
      rg2,
      rg3,
      rg4,
      rg5,
      rg6,
      rg7,
      rgNot0,
      rgNot1,
      rgNot2,
      rgNot3,
      rgNot4,
      rgNot5,
      rgNot6,
      rgNot7
    );
  public
    const Names: array[Enum] of String = (
      'rgPlaceholder',
      'rg0',
      'rg1',
      'rg2',
      'rg3',
      'rg4',
      'rg5',
      'rg6',
      'rg7',
      'rgNot0',
      'rgNot1',
      'rgNot2',
      'rgNot3',
      'rgNot4',
      'rgNot5',
      'rgNot6',
      'rgNot7'
    );
  public
    const JSONStrings: array[Enum] of String = (
      'placeholder',
      '0',
      '1',
      '2',
      '3',
      '4',
      '5',
      '6',
      '7',
      '!0',
      '!1',
      '!2',
      '!3',
      '!4',
      '!5',
      '!6',
      '!7'
    );
  end;
  TZYFilterModrmReg = TZYEnumFilterModrmReg.Enum;

  TZYEnumFilterModrmRm = record
  public
    type Enum = (
      rmPlaceholder,
      rm0,
      rm1,
      rm2,
      rm3,
      rm4,
      rm5,
      rm6,
      rm7,
      rmNot0,
      rmNot1,
      rmNot2,
      rmNot3,
      rmNot4,
      rmNot5,
      rmNot6,
      rmNot7
    );
  public
    const Names: array[Enum] of String = (
      'rmPlaceholder',
      'rm0',
      'rm1',
      'rm2',
      'rm3',
      'rm4',
      'rm5',
      'rm6',
      'rm7',
      'rmNot0',
      'rmNot1',
      'rmNot2',
      'rmNot3',
      'rmNot4',
      'rmNot5',
      'rmNot6',
      'rmNot7'
    );
  public
    const JSONStrings: array[Enum] of String = (
      'placeholder',
      '0',
      '1',
      '2',
      '3',
      '4',
      '5',
      '6',
      '7',
      '!0',
      '!1',
      '!2',
      '!3',
      '!4',
      '!5',
      '!6',
      '!7'
    );
  end;
  TZYFilterModrmRm = TZYEnumFilterModrmRm.Enum;

  TZYEnumFilterMandatoryPrefix = record
  public
    type Enum = (
      mpPlaceholder,
      mpIgnore,
      mpNone,
      mp66,
      mpF3,
      mpF2
    );
  public
    const Names: array[Enum] of String = (
      'mpPlaceholder',
      'mpIgnore',
      'mpNone',
      'mp66',
      'mpF3',
      'mpF2'
    );
  public
    const JSONStrings: array[Enum] of String = (
      'placeholder',
      'ignore',
      'none',
      '66',
      'f3',
      'f2'
    );
  end;
  TZYFilterMandatoryPrefix = TZYEnumFilterMandatoryPrefix.Enum;

  TZYEnumFilterOperandSize = record
  public
    type Enum = (
      osPlaceholder,
      os16,
      os32,
      os64,
      osNot16,
      osNot32,
      osNot64
    );
  public
    const Names: array[Enum] of String = (
      'osPlaceholder',
      'os16',
      'os32',
      'os64',
      'osNot16',
      'osNot32',
      'osNot64'
    );
  public
    const JSONStrings: array[Enum] of String = (
      'placeholder',
      '16',
      '32',
      '64',
      '!16',
      '!32',
      '!64'
    );
  end;
  TZYFilterOperandSize = TZYEnumFilterOperandSize.Enum;

  TZYEnumFilterAddressSize = record
  public
    type Enum = (
      asPlaceholder,
      as16,
      as32,
      as64,
      asNot16,
      asNot32,
      asNot64
    );
  public
    const Names: array[Enum] of String = (
      'asPlaceholder',
      'as16',
      'as32',
      'as64',
      'asNot16',
      'asNot32',
      'asNot64'
    );
  public
    const JSONStrings: array[Enum] of String = (
      'placeholder',
      '16',
      '32',
      '64',
      '!16',
      '!32',
      '!64'
    );
  end;
  TZYFilterAddressSize = TZYEnumFilterAddressSize.Enum;

  TZYEnumFilterVectorLength = record
  public
    type Enum = (
      vlPlaceholder,
      vl128,
      vl256,
      vl512
    );
  public
    const Names: array[Enum] of String = (
      'vlPlaceholder',
      'vl128',
      'vl256',
      'vl512'
    );
  public
    const JSONStrings: array[Enum] of String = (
      'placeholder',
      '128',
      '256',
      '512'
    );
  end;
  TZYFilterVectorLength = TZYEnumFilterVectorLength.Enum;

  TZYEnumFilterRexW = record
  public
    type Enum = (
      rwPlaceholder,
      rwW0,
      rwW1
    );
  public
    const Names: array[Enum] of String = (
      'rwPlaceholder',
      'rwW0',
      'rwW1'
    );
  public
    const JSONStrings: array[Enum] of String = (
      'placeholder',
      '0',
      '1'
    );
  end;
  TZYFilterRexW = TZYEnumFilterRexW.Enum;

  TZYEnumFilterRexB = record
  public
    type Enum = (
      rbPlaceholder,
      rbB0,
      rbB1
    );
  public
    const Names: array[Enum] of String = (
      'rwPlaceholder',
      'rbB0',
      'rbB1'
    );
  public
    const JSONStrings: array[Enum] of String = (
      'placeholder',
      '0',
      '1'
    );
  end;
  TZYFilterRexB = TZYEnumFilterRexB.Enum;

  TZYEnumFilterEvexB = record
  public
    type Enum = (
      ebPlaceholder,
      ebB0,
      ebB1
    );
  public
    const Names: array[Enum] of String = (
      'ebPlaceholder',
      'ebB0',
      'ebB1'
    );
  public
    const JSONStrings: array[Enum] of String = (
      'placeholder',
      '0',
      '1'
    );
  end;
  TZYFilterEvexB = TZYEnumFilterEvexB.Enum;

  TZYEnumFilterEvexZ = record
  public
    type Enum = (
      ezPlaceholder,
      ezZ0,
      ezZ1
    );
  public
    const Names: array[Enum] of String = (
      'ezPlaceholder',
      'ezZ0',
      'ezZ1'
    );
  public
    const JSONStrings: array[Enum] of String = (
      'placeholder',
      '0',
      '1'
    );
  end;
  TZYFilterEvexZ = TZYEnumFilterEvexZ.Enum;

  TZYEnumFilterMvexE = record
  public
    type Enum = (
      mePlaceholder,
      meE0,
      meE1
    );
  public
    const Names: array[Enum] of String = (
      'mePlaceholder',
      'meE0',
      'meE1'
    );
  public
    const JSONStrings: array[Enum] of String = (
      'placeholder',
      '0',
      '1'
    );
  end;
  TZYFilterMvexE = TZYEnumFilterMvexE.Enum;

  TZYEnumFilterBoolean = record
  public
    type Enum = (
      fbPlaceholder,
      fbB0,
      fbB1
    );
  public
    const Names: array[Enum] of String = (
      'fbPlaceholder',
      'fbB0',
      'fbB1'
    );
  public
    const JSONStrings: array[Enum] of String = (
      'placeholder',
      '0',
      '1'
    );
  end;
  TZYFilterBoolean = TZYEnumFilterBoolean.Enum;

  TZYEnumFilterEvexND = record
  public
    type Enum = (
      ndPlaceholder,
      ndND0,
      ndND1
    );
  public
    const Names: array[Enum] of String = (
      'ndPlaceholder',
      'ndND0',
      'ndND1'
    );
  public
    const JSONStrings: array[Enum] of String = (
      'placeholder',
      '0',
      '1'
    );
  end;
  TZYFilterEvexND = TZYEnumFilterEvexND.Enum;

  TZYEnumFilterEvexNF = record
  public
    type Enum = (
      nfPlaceholder,
      nfNF0,
      nfNF1
    );
  public
    const Names: array[Enum] of String = (
      'nfPlaceholder',
      'nfNF0',
      'nfNF1'
    );
  public
    const JSONStrings: array[Enum] of String = (
      'placeholder',
      '0',
      '1'
    );
  end;
  TZYFilterEvexNF = TZYEnumFilterEvexNF.Enum;

  TZYEnumFilterEvexSCC = record
  public
    type Enum = (
      sccPlaceholder,
      sccSCC0,
      sccSCC1,
      sccSCC2,
      sccSCC3,
      sccSCC4,
      sccSCC5,
      sccSCC6,
      sccSCC7,
      sccSCC8,
      sccSCC9,
      sccSCC10,
      sccSCC11,
      sccSCC12,
      sccSCC13,
      sccSCC14,
      sccSCC15
    );
  public
    const Names: array[Enum] of String = (
      'sccPlaceholder',
      'sccSCC0',
      'sccSCC1',
      'sccSCC2',
      'sccSCC3',
      'sccSCC4',
      'sccSCC5',
      'sccSCC6',
      'sccSCC7',
      'sccSCC8',
      'sccSCC9',
      'sccSCC10',
      'sccSCC11',
      'sccSCC12',
      'sccSCC13',
      'sccSCC14',
      'sccSCC15'
    );
  public
    const JSONStrings: array[Enum] of String = (
      'placeholder',
      '0',
      '1',
      '2',
      '3',
      '4',
      '5',
      '6',
      '7',
      '8',
      '9',
      '10',
      '11',
      '12',
      '13',
      '14',
      '15'
    );
  end;
  TZYFilterEvexSCC = TZYEnumFilterEvexSCC.Enum;

  TZYEnumFilterRex2 = record
  public
    type Enum = (
      r2Placeholder,
      r2NoRex2,
      r2Rex2
    );
  public
    const Names: array[Enum] of String = (
      'r2Placeholder',
      'r2NoRex2',
      'r2Rex2'
    );
  public
    const JSONStrings: array[Enum] of String = (
      'placeholder',
      'no_rex2',
      'rex2'
    );
  end;
  TZYFilterRex2 = TZYEnumFilterRex2.Enum;

implementation

end.
