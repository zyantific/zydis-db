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

unit Zydis.Validator;

interface

uses
  System.Generics.Collections, Zydis.InstructionEditor;

type
  TZYDefinitionValidator = record
  strict private
    class function ValidateOperands(Definition: TZYInstructionDefinition;
      Operands: TZYInstructionOperands; GenerateErrorMessages: Boolean;
      ErrorMessages: TList<String>): Boolean; static;
    class function ValidateOperand(Definition: TZYInstructionDefinition;
      Operand: TZYInstructionOperand; GenerateErrorMessages: Boolean;
      ErrorMessages: TList<String>): Boolean; static;
  strict private
    class function Validate(Definition: TZYInstructionDefinition; GenerateErrorMessages: Boolean;
      ErrorMessages: TList<String>): Boolean; overload; static;
  public
    class function Validate(Definition: TZYInstructionDefinition): Boolean; overload; static;
    class function Validate(Definition: TZYInstructionDefinition;
      var ErrorMessages: TArray<String>): Boolean; overload; static;
  end;

implementation

uses
  System.SysUtils, Zydis.Enums, Zydis.Enums.Filters, Zydis.InstructionFilters;

{ TZYDefinitionValidator }

class function TZYDefinitionValidator.Validate(Definition: TZYInstructionDefinition;
  GenerateErrorMessages: Boolean; ErrorMessages: TList<String>): Boolean;
var
  B: Boolean;
begin
  Result := (Definition.PrivilegeLevel in [0, 3]);
  if (not Result) then
  begin
    if (GenerateErrorMessages) then
    begin
      ErrorMessages.Add('Invalid CPU-privilege-level. Allowed values are [0, 3].');
    end else Exit;
  end;

  B := (Definition.Filters.ForceModrmReg and
    (not (Definition.Filters.ModrmReg in [rg0, rg1, rg2, rg3, rg4, rg5, rg6, rg7])));
  if (B) then
  begin
    Result := false;
    if (GenerateErrorMessages) then
    begin
      ErrorMessages.Add('ForceModrmReg');
    end else Exit;
  end;

  B := (Definition.Filters.ForceModrmRm and
    (not (Definition.Filters.ModrmRm in [rm0, rm1, rm2, rm3, rm4, rm5, rm6, rm7])));
  if (B) then
  begin
    Result := false;
    if (GenerateErrorMessages) then
    begin
      ErrorMessages.Add('ForceModrmRm');
    end else Exit;
  end;

  B := (Definition.EVEX.Functionality in [evRC, evSAE]) and
    ((Definition.EVEX.VectorLength = vlDefault) or
    (Definition.Filters.VectorLength <> vlPlaceholder));
  if (B) then
  begin
    Result := false;
    if (GenerateErrorMessages) then
    begin
      ErrorMessages.Add('xxx');
    end else Exit;
  end;

  // TODO:
  {
  B := (Definition.Encoding = iencEVEX) and (Definition.Filters.ModrmMod <> md3) and
    ((Definition.EVEX.TupleType = ttInvalid) or (Definition.EVEX.ElementSize = esInvalid));
  if (B) then
  begin
    Result := false;
    if (GenerateErrorMessages) then
    begin
      ErrorMessages.Add('yyy');
    end else Exit;
  end;
  }

  B := (Definition.Encoding = iencEVEX) and (Definition.Filters.EvexB = ebB1) and
    (Definition.Filters.VectorLength <> vlPlaceholder);
  if (B) then
  begin
    Result := false;
    if (GenerateErrorMessages) then
    begin
      ErrorMessages.Add('zzz');
    end else Exit;
  end;

  B := (Definition.Encoding = iencMVEX) and (Definition.Filters.ModrmMod <> md3) and
    ((Definition.MVEX.Functionality = mvIgnored) or (Definition.MVEX.Functionality = mvInvalid));
  if (B) then
  begin
    Result := false;
    if (GenerateErrorMessages) then
    begin
      ErrorMessages.Add('asdasd');
    end else Exit;
  end;

  B := (Definition.Encoding = iencMVEX) and (Definition.MVEX.StaticBroadcast <> sbcNone);
  if B then
  begin
    case Definition.MVEX.StaticBroadcast of
      sbcBroadcast1to8,
      sbcBroadcast4to8 : B := not (Definition.MVEX.Functionality in [
        mvf64, mvi64, mvSf64, mvSi64, mvUf64, mvUi64, mvDf64, mvDi64]);
      sbcBroadcast1to16,
      sbcBroadcast4to16: B := not (Definition.MVEX.Functionality in [
        mvf32, mvi32, mvSf32, mvSi32, mvUf32, mvUi32, mvDf32, mvDi32]);
    end;
    B := B or (Definition.MVEX.HasElementGranularity)
  end;
  if (B) then
  begin
    Result := false;
    if (GenerateErrorMessages) then
    begin
      ErrorMessages.Add('sdfse4swdf');
    end else Exit;
  end;

  B := (Definition.Encoding = iencEVEX) and (Definition.Filters.VectorLength = vlPlaceholder) and
    (Definition.EVEX.VectorLength = vlDefault);
  if (B) then
  begin
    Result := false;
    if (GenerateErrorMessages) then
    begin
      ErrorMessages.Add('vsdfsdf');
    end else Exit;
  end;

  if (Definition.Encoding = iencEVEX) then
  begin
    B := false;
    case Definition.ExceptionClass of
      ecNone: B := true;
      ecE1,
      ecE1NF:
        B := (Definition.Filters.EvexB <> ebB0);
      ecE5NF:
        B := (Definition.Filters.EvexB <> ebB0);
      ecE6,
      ecE6NF:
        B := (Definition.Filters.EvexB <> ebB0);
      ecE7NM:
        B := (Definition.Filters.EvexB <> ebB0); // TODO: Instr specific LL
      ecE7NM128:
        B := (Definition.Filters.EvexB <> ebB0) or (Definition.Filters.VectorLength <> vl128);
      ecE9NF:
        B := (Definition.Filters.EvexB <> ebB0) or (Definition.Filters.VectorLength <> vl128);
      ecE10,
      ecE10NF:
        B := false;
      ecE12,
      ecE12NP:
        B := (Definition.Filters.EvexB <> ebB0);
    end;
    if (B) then
    begin
      Result := false;
      if (GenerateErrorMessages) then
      begin
        ErrorMessages.Add('Exception-class specific condition violated.');
      end else Exit;
    end;
  end;

  Result := ValidateOperands(
    Definition, Definition.Operands, GenerateErrorMessages, ErrorMessages) and Result;
end;

class function TZYDefinitionValidator.Validate(Definition: TZYInstructionDefinition): Boolean;
begin
  Result := Validate(Definition, false, nil);
end;

class function TZYDefinitionValidator.Validate(Definition: TZYInstructionDefinition;
  var ErrorMessages: TArray<String>): Boolean;
var
  List: TList<String>;
begin
  List := TList<String>.Create;
  try
    Result := Validate(Definition, true, List);
    ErrorMessages := List.ToArray;
  finally
    List.Free;
  end;
end;

class function TZYDefinitionValidator.ValidateOperand(Definition: TZYInstructionDefinition;
  Operand: TZYInstructionOperand; GenerateErrorMessages: Boolean;
  ErrorMessages: TList<String>): Boolean;
type
  TZYOperandEncodingSet = set of TZYOperandEncoding;
const
  ValidEncodings: array[TZYOperandType] of TZYOperandEncodingSet =
  (
    { optUnused }
    [opeNone],
    { optImplicitReg }
    [opeNone],
    { optImplicitMem }
    [opeNone],
    { optImplicitImm1 }
    [opeNone],
    { optGPR8 }
    [opeModrmReg, opeModrmRm, opeNDSNDD, opeOpcodeBits],
    { optGPR16 }
    [opeModrmReg, opeModrmRm, opeNDSNDD, opeOpcodeBits],
    { optGPR32 }
    [opeModrmReg, opeModrmRm, opeNDSNDD, opeOpcodeBits],
    { optGPR64 }
    [opeModrmReg, opeModrmRm, opeNDSNDD, opeOpcodeBits],
    { optGPR16_32_64 }
    [opeModrmReg, opeModrmRm, opeNDSNDD, opeOpcodeBits],
    { optGPR32_32_64 }
    [opeModrmReg, opeModrmRm, opeNDSNDD, opeOpcodeBits],
    { optGPR16_32_32 }
    [opeModrmReg, opeModrmRm, opeNDSNDD, opeOpcodeBits],
    { optGPRASZ }
    [opeModrmReg, opeModrmRm],
    { optFPR }
    [opeModrmRm],
    { optMMX }
    [opeModrmReg, opeModrmRm],
    { optXMM }
    [opeModrmReg, opeModrmRm, opeNDSNDD, opeIS4],
    { optYMM }
    [opeModrmReg, opeModrmRm, opeNDSNDD, opeIS4],
    { optZMM }
    [opeModrmReg, opeModrmRm, opeNDSNDD],
    { optTMM }
    [opeModrmReg, opeModrmRm, opeNDSNDD],
    { optBND }
    [opeModrmReg, opeModrmRm],
    { optSREG }
    [opeModrmReg],
    { optCR }
    [opeModrmReg],
    { optDR }
    [opeModrmReg],
    { optMASK }
    [opeMASK, opeModrmReg, opeModrmRm, opeNDSNDD],
    { optMEM }
    [opeModrmRm],
    { optMEMVSIBX }
    [opeModrmRm],
    { optMEMVSIBY }
    [opeModrmRm],
    { optMEMVSIBZ }
    [opeModrmRm],
    { optIMM }
    [opeIS4,
     opeUImm8, opeUImm16, opeUImm32, opeUImm64,
     opeUImm16_32_64,
     opeUImm32_32_64,
     opeUImm16_32_32,
     opeSImm8, opeSImm16, opeSImm32, opeSImm64,
     opeSImm16_32_64,
     opeSImm32_32_64,
     opeSImm16_32_32],
    { optREL }
    [opeJImm8, opeJImm16, opeJImm32, opeJImm64,
     opeJImm16_32_64,
     opeJImm32_32_64,
     opeJImm16_32_32],
    { optPTR }
    [opeNone],
    { optAGEN }
    [opeModrmRm],
    { optAGENNoRel }
    [opeModrmRm],
    { optMOFFS }
    [opeDisp16_32_64],
    { optMIB }
    [opeModrmRm],
    { optDFV }
    [opeNDSNDD]
  );
var
  B: Boolean;
begin
  Result := false;

  // Invalid operand encodings
  if (Operand.OperandType <> optUnused) and
    (not (Operand.Encoding in ValidEncodings[Operand.OperandType])) then
  begin
    if (GenerateErrorMessages) then
    begin
      ErrorMessages.Add(Format('Operand #%d: ' +
        'The %s operand-encoding is not allowed for the %s operand-type.',
        [Operand.Index, TZYEnumOperandEncoding.ZydisStrings[Operand.Encoding],
        TZYEnumOperandType.ZydisStrings[Operand.OperandType]]));
    end else Exit;
  end;

  // Invalid operand size
  {if (Operand.OperandType in [optImplicitMem, optMEM, optMEMVSIBX, optMEMVSIBY, optMEMVSIBZ,
    optIMM, optREL, optPTR, optMOFFS]) then
  begin
    if ((Operand.Width16 = 0) or (Operand.Width32 = 0) or (Operand.Width64 = 0)) then
    begin

    end;

    if (GenerateErrorMessages) then
    begin
      ErrorMessages.Add(Format('Operand #%d: ' +
        'The %s operand-type requires an explicit operand size.',
        [Operand.Index, TZYEnumOperandType.ZydisStrings[Operand.OperandType]]));
    end else Exit;
  end;
  if (Operand.OperandType = optAGEN) then
  begin

  end;}

(*

  // Invalid operand width
  B := false;
  case Operand.OperandType of
    optUnused: ;
    optImplicitReg: ;
    optImplicitMem: ;
    optGPR8,
    optGPR16,
    optGPR32,
    optGPR64,
    optGPR16_32_64,
    optGPR32_32_64:
      begin
        B := (not (Operand.Width16 in [8, 16, 32, 64])) or
             (not (Operand.Width32 in [8, 16, 32, 64])) or
             (not (Operand.Width64 in [8, 16, 32, 64]));
      end;
    optFPR: ;
    optMMX: ;
    optXMM: ;
    optYMM: ;
    optZMM: ;
    optBND: ;
    optSREG: ;
    optCR: ;
    optDR: ;
    optMASK: ;
    optMem: ;
    optMemBCST2: ;
    optMemBCST4: ;
    optMemBCST8: ;
    optMemBCST16: ;
    optMemVSIBX: ;
    optMemVSIBY: ;
    optMemVSIBZ: ;
    optImm8,
    optImm16,
    optImm32,
    optImm64,
    optImm16_32_64,
    optImm32_32_64:
      begin
        B := (not (Operand.Width16 in [8, 16, 32, 64])) or
             (not (Operand.Width32 in [8, 16, 32, 64])) or
             (not (Operand.Width64 in [8, 16, 32, 64]));
      end;
    optRel8,
    optRel16,
    optRel32,
    optRel64: ;
    optPointer: ;
    optMOFFS16: ;
    optMOFFS32: ;
    optMOFFS64: ;
  end;
  if (B) then
  begin
    if (GenerateErrorMessages) then
    begin
      ErrorMessages.Add(Format('Operand #%d: Invalid operand width.', [Operand.Index]));
    end else Exit;
  end;

  // Invalid register value
  if (Operand.Register <> regNone) then
  begin
    if (not (Operand.OperandType in [optImplicitReg, optImplicitMem])) then
    begin
      if (GenerateErrorMessages) then
      begin
        ErrorMessages.Add(Format('Operand #%d: ' +
          'An explicit register value is only valid for IMPLICIT_REG and IMPLICIT_MEM ' +
          'operands.', [Operand.Index]));
      end else Exit;
    end;
  end else
  begin
    if (Operand.OperandType in [optImplicitReg, optImplicitMem]) then
    begin
      if (GenerateErrorMessages) then
      begin
        ErrorMessages.Add(Format('Operand #%d: ' +
          'IMPLICIT_REG and IMPLICIT_MEM operands require an explicit register value.',
          [Operand.Index]));
      end else Exit;
    end;
  end;

  // Invalid segment value
  if (Operand.Segment <> regNone) then
  begin
    if (not (Operand.OperandType in [optImplicitMem])) then
    begin
      if (GenerateErrorMessages) then
      begin
        ErrorMessages.Add(Format('Operand #%d: ' +
          'An explicit segment value is only valid for IMPLICIT_MEM operands.', [Operand.Index]));
      end else Exit;
    end;
  end else
  begin
    if (Operand.OperandType in [optImplicitMem]) then
    begin
      if (GenerateErrorMessages) then
      begin
        ErrorMessages.Add(Format('Operand #%d: ' +
          'IMPLICIT_MEM operands require an explicit segment value.', [Operand.Index]));
      end else Exit;
    end;
  end;
*)

  // Invalid MASK size
  if (Operand.OperandType = optMASK) then
  begin
    B := true;
    case Definition.Encoding of
      iencDEFAULT,
      ienc3DNOW,
      iencXOP,
      iencVEX : B := false;
      iencEVEX:
        B := not (((Operand.Width16 = 0) and (Operand.Width32 = 0) and (Operand.Width64 = 0)) or
                  ((Operand.Width16 = 8) and (Operand.Width32 = 8) and (Operand.Width64 = 8)));
      iencMVEX:
        B := not (((Operand.Width16 = 0) and (Operand.Width32 = 0) and (Operand.Width64 = 0)) or
                  ((Operand.Width16 = 2) and (Operand.Width32 = 2) and (Operand.Width64 = 2)));
    end;
    if (B) then
    begin
      if (GenerateErrorMessages) then
      begin
        ErrorMessages.Add('axa');
      end else Exit;
    end;
  end;

  Result := true;
end;

class function TZYDefinitionValidator.ValidateOperands(Definition: TZYInstructionDefinition;
  Operands: TZYInstructionOperands; GenerateErrorMessages: Boolean;
  ErrorMessages: TList<String>): Boolean;
const
  VisibilityMapping: array[TZYOperandVisibility] of Byte =
  (
    { opvExplicit } 1,
    { opvImplicit } 1,
    { opvHidden   } 2,
    { opvUnused   } 3
  );
var
  I: Integer;
  EncReg, EncRm, EncVVVV, EncAAA: Integer;
begin
  Result := false;

  // Invalid operand order
  for I := 0 to Operands.Count - 2 do
  begin
    if (VisibilityMapping[Operands.Items[I].Visibility] >
      VisibilityMapping[Operands.Items[I + 1].Visibility]) then
    begin
      if (GenerateErrorMessages) then
      begin
        ErrorMessages.Add('Invalid operand order. EXPLICIT = IMPLICIT > HIDDEN > UNUSED.');
        Break;
      end else Exit;
    end;
  end;

  // Invalid encoding combinations
  // TODO: Check all encodings
  EncReg := 0; EncRm := 0; EncVVVV := 0; EncAAA := 0;
  for I := 0 to Operands.Count - 1 do
  begin
    case Operands.Items[I].Encoding of
      opeModrmReg   :
        begin
          Inc(EncReg);
          if (EncReg = 2) then
          begin
            if (GenerateErrorMessages) then
            begin
              ErrorMessages.Add('Multiple operands are using the MODRM.REG encoding.');
              Break;
            end else Exit;
          end;
        end;
      opeModrmRm:
        begin
          Inc(EncRm);
          if (EncRm >= 2) then
          begin
            if (Definition.Mnemonic = 'nop') and (Definition.Encoding = iencDefault) and
              (Definition.OpcodeMap = omap0F) and (Definition.Opcode in [$1A, $1B]) then
            begin
              // These WIDENOP instructions are using multiple operands with MODRM.RM encoding
              Continue;
            end;
            if (GenerateErrorMessages) then
            begin
              ErrorMessages.Add('Multiple operands are using the MODRM.RM encoding.');
              Break;
            end else Exit;
          end;
        end;
      opeNDSNDD     :
        begin
          Inc(EncVVVV);
          if (EncVVVV = 2) then
          begin
            if (GenerateErrorMessages) then
            begin
              ErrorMessages.Add('Multiple operands are using the VEX/EVEX/MVEX.VVVV encoding.');
              Break;
            end else Exit;
          end;
        end;
      opeMASK       :
        begin
          Inc(EncAAA);
          if (EncAAA = 2) then
          begin
            if (GenerateErrorMessages) then
            begin
              ErrorMessages.Add('Multiple operands are using the EVEX/MVEX.AAA encoding.');
              Break;
            end else Exit;
          end;
        end;
    end;
  end;

  {if (Operands.Definition.Filters.ModrmMod <> mdPlaceholder) and (EncReg > 0) and (EncRm = 0) then
  begin
    if (GenerateErrorMessages) then
    begin
      ErrorMessages.Add('TODO:');
    end else Exit;
  end;}

  // Validate individual operands
  for I := 0 to Operands.Count - 1 do
  begin
    if (not ValidateOperand(Definition, Operands.Items[I], GenerateErrorMessages,
      ErrorMessages)) then
    begin
      if (not GenerateErrorMessages) then Exit;
    end;
  end;

  Result := true;
end;

end.
