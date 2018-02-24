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

unit Zydis.InstructionFilters;

interface

uses
  Zydis.Enums.Filters;

type
  TZYInstructionFilterClass = (
    ifcInvalid,
    ifcXOP,
    ifcVEX,
    ifcEMVEX,
    ifcOpcode,
    ifcMode,
    ifcModeCompact,
    ifcModrmMod,
    ifcModrmModCompact,
    ifcModrmReg,
    ifcModrmRm,
    ifcMandatoryPrefix,
    ifcOperandSize,
    ifcAddressSize,
    ifcVectorLength,
    ifcRexW,
    ifcRexB,
    ifcEvexB,
    ifcMvexE,
    ifcModeAMD,
    ifcModeKNC,
    ifcModeMPX,
    ifcModeCET,
    ifcModeLZCNT,
    ifcModeTZCNT,
    ifcModeWBNOINVD
  );

  TZYInstructionFilterFlag = (
    {**
     * This filter is only used by the instruction-editor.
     * }
    iffIsEditorOnly,
    {**
     * This filter is only used by the code-generator.
     * }
    iffIsCodeGenOnly,
    {**
     * This flag marks the filter as optional. If a definition does not specify a value for an
     * optional filter, it gets inserted into a special placeholder-slot.
     *
     * Optional filters with only the placeholder-slot set, will be optimized away.
     *
     * Optional filters with the placeholder-slot and at least one regular slot set, will signal
     * a conflict.
     * }
    iffIsOptional,
    {**
     * This filter supports negated values. There should be a negated value for every regular value
     * in the filter.
     *
     * Filters with negated-value support will signal a conflict, if multiple negated values are
     * set.
     *
     * Filters with negated-value support will signal a conflict, if one negated value and at last
     * one regular value, which is not the counterpart of the negated value, is set.
     * }
    iffNegatedValues,
    {**
     * This filter can be compacted by the code-generator.
     *
     * Some filters like MODE or MODRM.MOD got a lot of different values, but most of the time
     * only two opposing ones really used.
     * In this case, the code-generator can compress the filter by replacing it with a compacted
     * version.
     * }
    iffIsCompactable
  );
  TZYInstructionFilterFlags = set of TZYInstructionFilterFlag;

  PZYInstructionFilter = ^TZYInstructionFilter;
  TZYInstructionFilter = record
  strict private
    FFilterClass: TZYInstructionFilterClass;
    FIndexPlaceholder: Integer;
    FIndexValueLo: Integer;
    FIndexValueHi: Integer;
    FIndexNegatedValueLo: Integer;
    FIndexNegatedValueHi: Integer;
    FTotalCapacity: Cardinal;
  private
    FNumberOfValues: Cardinal;
    FFlags: TZYInstructionFilterFlags;
    FCompactFilterClass: TZYInstructionFilterClass;
    FCompactFilterValue: Integer;
  private
    constructor Create(FilterClass: TZYInstructionFilterClass; NumberOfValues: Cardinal;
      Flags: TZYInstructionFilterFlags;
      CompactFilterClass: TZYInstructionFilterClass = ifcInvalid; CompactFilterValue: Integer = -1);
  public
    function IndexContainsValue(Index: Integer): Boolean; inline;
    function IndexContainsNegatedValue(Index: Integer): Boolean; inline;
    function IsEditorOnly: Boolean; inline;
    function IsCodeGenOnly: Boolean; inline;
    function IsOptional: Boolean; inline;
    function SupportsNegatedValues: Boolean; inline;
    function IsCompactable: Boolean; inline;
  public
    property FilterClass: TZYInstructionFilterClass read FFilterClass;
    {**
     * The number of normal filter-values. The extra slots for the optional placeholder and-/or
     * negated-values are not included in this number.
     * }
    property NumberOfValues: Cardinal read FNumberOfValues;
    property Flags: TZYInstructionFilterFlags read FFlags;
    property IndexPlaceholder: Integer read FIndexPlaceholder;
    property IndexValueLo: Integer read FIndexValueLo;
    property IndexValueHi: Integer read FIndexValueHi;
    property IndexNegatedValueLo: Integer read FIndexNegatedValueLo;
    property IndexNegatedValueHi: Integer read FIndexNegatedValueHi;
    property TotalCapacity: Cardinal read FTotalCapacity;
    {**
     *
     * }
    property CompactFilterClass: TZYInstructionFilterClass read FCompactFilterClass;
    {**
     *
     * }
    property CompactFilterValue: Integer read FCompactFilterValue;
  end;

  TZYInstructionFilterList = TArray<TZYInstructionFilterClass>;

  TZYInstructionFilterInfo = record
  strict private
    const FilterInfo: array[TZYInstructionFilterClass] of TZYInstructionFilter =
    (
      { ifcInvalid }
      (FNumberOfValues:   0; FFlags: [iffIsEditorOnly]),
      { ifcXOP }
      (FNumberOfValues:  13; FFlags: []),
      { ifcVEX }
      (FNumberOfValues:  17; FFlags: []),
      { ifcEMVEX }
      (FNumberOfValues:  33; FFlags: []),
      { ifcOpcode }
      (FNumberOfValues: 256; FFlags: []),
      { ifcMode }
      (FNumberOfValues:   3; FFlags: [iffIsOptional, iffNegatedValues, iffIsCompactable];
       FCompactFilterClass: ifcModeCompact    ; FCompactFilterValue: 2),
      { ifcModeCompact }
      (FNumberOfValues:   2; FFlags: [iffIsCodeGenOnly]),
      { ifcModrmMod }
      (FNumberOfValues:   4; FFlags: [iffIsOptional, iffNegatedValues, iffIsCompactable];
       FCompactFilterClass: ifcModrmModCompact; FCompactFilterValue: 3),
      { ifcModrmModCompact }
      (FNumberOfValues:   2; FFlags: [iffIsCodeGenOnly]),
      { ifcModrmReg }
      (FNumberOfValues:   8; FFlags: [iffIsOptional, iffNegatedValues]),
      { ifcModrmRm }
      (FNumberOfValues:   8; FFlags: [iffIsOptional, iffNegatedValues]),
      { ifcMandatoryPrefix }
      (FNumberOfValues:   5; FFlags: [iffIsOptional]),
      { ifcOperandSize }
      (FNumberOfValues:   3; FFlags: [iffIsOptional, iffNegatedValues]),
      { ifcAddressSize }
      (FNumberOfValues:   3; FFlags: [iffIsOptional, iffNegatedValues]),
      { ifcVectorLength }
      (FNumberOfValues:   3; FFlags: [iffIsOptional]),
      { ifcRexW }
      (FNumberOfValues:   2; FFlags: [iffIsOptional]),
      { ifcRexB }
      (FNumberOfValues:   2; FFlags: [iffIsOptional]),
      { ifcEvexB }
      (FNumberOfValues:   2; FFlags: [iffIsOptional]),
      { ifcMvexE }
      (FNumberOfValues:   2; FFlags: [iffIsOptional]),
      { ifcModeAMD }
      (FNumberOfValues:   2; FFlags: [iffIsOptional]),
      { ifcModeKNC }
      (FNumberOfValues:   2; FFlags: [iffIsOptional]),
      { ifcModeMPX }
      (FNumberOfValues:   2; FFlags: [iffIsOptional]),
      { ifcModeCET }
      (FNumberOfValues:   2; FFlags: [iffIsOptional]),
      { ifcModeLZCNT }
      (FNumberOfValues:   2; FFlags: [iffIsOptional]),
      { ifcModeTZCNT }
      (FNumberOfValues:   2; FFlags: [iffIsOptional]),
      { ifcModeWBNOINVD }
      (FNumberOfValues:   2; FFlags: [iffIsOptional])
    );
  strict private
    class var FilterOrderDef : TZYInstructionFilterList;
    class var FilterOrderXOP : TZYInstructionFilterList;
    class var FilterOrderVEX : TZYInstructionFilterList;
    class var FilterOrderEVEX: TZYInstructionFilterList;
    class var FilterOrderMVEX: TZYInstructionFilterList;
  strict private
    class function GetFilterInfo(
      FilterClass: TZYInstructionFilterClass): TZYInstructionFilter; static; inline;
    class function GetFilterOrder(
      Encoding: TZYInstructionEncoding): TZYInstructionFilterList; static; inline;
  public
    class constructor Create;
  public
    class property Info[FilterClass: TZYInstructionFilterClass]: TZYInstructionFilter
      read GetFilterInfo;
    class property FilterOrder[Encoding: TZYInstructionEncoding]: TZYInstructionFilterList
      read GetFilterOrder;
  end;

implementation

uses
  Utils.Container;

{ TZYInstructionFilter }

constructor TZYInstructionFilter.Create(FilterClass: TZYInstructionFilterClass;
  NumberOfValues: Cardinal; Flags: TZYInstructionFilterFlags;
  CompactFilterClass: TZYInstructionFilterClass; CompactFilterValue: Integer);
begin
  FFilterClass := FilterClass;
  FFlags := Flags;
  FNumberOfValues := NumberOfValues;
  FCompactFilterClass := CompactFilterClass;
  FCompactFilterValue := CompactFilterValue;

  FIndexPlaceholder := -1;
  FIndexValueLo := 0;
  FIndexValueHi := 0;
  if (FNumberOfValues > 0) then
  begin
    FIndexValueHi := FNumberOfValues - 1;
  end;
  FIndexNegatedValueLo := -1;
  FIndexNegatedValueHi := -1;
  FTotalCapacity := NumberOfValues;

  if (iffIsOptional in Flags) then
  begin
    FIndexPlaceholder := 0;
    Inc(FIndexValueLo);
    Inc(FIndexValueHi);
    Inc(FTotalCapacity);
  end;

  if (iffNegatedValues in Flags) then
  begin
    Assert(NumberOfValues > 1);
    FIndexNegatedValueLo := FIndexValueHi + 1;
    {$WARNINGS OFF}
    FIndexNegatedValueHi := FIndexValueHi + FNumberOfValues;
    {$WARNINGS ON}
    Inc(FTotalCapacity, FNumberOfValues);
  end;

  if (iffIsCompactable in Flags) then
  begin
    Assert(SupportsNegatedValues);
    Assert(TZYInstructionFilterInfo.Info[CompactFilterClass].Flags = [iffIsCodeGenOnly]);
    Assert(TZYInstructionFilterInfo.Info[CompactFilterClass].NumberOfValues = 2);
    Assert(CompactFilterValue >= 0);
  end;
end;

function TZYInstructionFilter.IndexContainsNegatedValue(Index: Integer): Boolean;
begin
  Result := (Index >= FIndexNegatedValueLo) and (Index <= FIndexNegatedValueHi);
end;

function TZYInstructionFilter.IndexContainsValue(Index: Integer): Boolean;
begin
  Result := (Index >= FIndexValueLo) and (Index <= FIndexValueHi)
end;

function TZYInstructionFilter.IsCodeGenOnly: Boolean;
begin
  Result := (iffIsCodeGenOnly in FFlags);
end;

function TZYInstructionFilter.IsCompactable: Boolean;
begin
  Result := (iffIsCompactable in FFlags);
end;

function TZYInstructionFilter.IsEditorOnly: Boolean;
begin
  Result := (iffIsEditorOnly in FFlags);
end;

function TZYInstructionFilter.IsOptional: Boolean;
begin
  Result := (iffIsOptional in FFlags);
end;

function TZYInstructionFilter.SupportsNegatedValues: Boolean;
begin
  Result := (iffNegatedValues in FFlags);
end;

{ TZYInstructionFilterInfo }

class constructor TZYInstructionFilterInfo.Create;
var
  FilterClass: TZYInstructionFilterClass;
begin
  for FilterClass := Low(FilterInfo) to High(FilterInfo) do
  begin
    FilterInfo[FilterClass].Create(FilterClass, FilterInfo[FilterClass].NumberOfValues,
      FilterInfo[FilterClass].Flags, FilterInfo[FilterClass].CompactFilterClass,
      FilterInfo[FilterClass].CompactFilterValue);
  end;
  FilterOrderDef :=
    TZYInstructionFilterList.Create(
      ifcModeMPX,
      ifcModrmMod,
      ifcMandatoryPrefix,
      ifcModrmReg,
      ifcModrmRm,
      ifcMode,
      ifcAddressSize,
      ifcOperandSize,
      ifcRexW,
      ifcRexB,
      ifcModeAMD,
      ifcModeKNC,
      ifcModeCET,
      ifcModeLZCNT,
      ifcModeTZCNT,
      ifcModeWBNOINVD
    );
  FilterOrderXOP :=
    TZYInstructionFilterList.Create(
      ifcOpcode,
      ifcModrmReg,
      ifcModrmRm,
      ifcVectorLength,
      ifcMode,
      ifcModrmMod,
      ifcRexW,
      ifcOperandSize,
      ifcAddressSize
    );
  FilterOrderVEX :=
    TZYInstructionFilterList.Create(
      ifcOpcode,
      ifcModrmReg,
      ifcModrmRm,
      ifcVectorLength,
      ifcMode,
      ifcModrmMod,
      ifcRexW,
      ifcOperandSize,
      ifcAddressSize,
      ifcModeKNC
    );
  FilterOrderEVEX :=
    TZYInstructionFilterList.Create(
      ifcOpcode,
      ifcModrmMod,
      ifcModrmReg,
      ifcModrmRm,
      ifcRexW,
      ifcMode,
      ifcOperandSize,
      ifcAddressSize,
      ifcEvexB,
      ifcVectorLength
    );
  FilterOrderMVEX :=
    TZYInstructionFilterList.Create(
      ifcOpcode,
      ifcModrmMod,
      ifcModrmReg,
      ifcModrmRm,
      ifcRexW,
      ifcMode,
      ifcOperandSize,
      ifcAddressSize,
      ifcMvexE,
      ifcVectorLength
    );
end;

class function TZYInstructionFilterInfo.GetFilterInfo(
  FilterClass: TZYInstructionFilterClass): TZYInstructionFilter;
begin
  Result := FilterInfo[FilterClass];
end;

class function TZYInstructionFilterInfo.GetFilterOrder(
  Encoding: TZYInstructionEncoding): TZYInstructionFilterList;
begin
  Result := FilterOrderDef;
  case Encoding of
    iencXOP : Result := FilterOrderXOP;
    iencVEX : Result := FilterOrderVEX;
    iencEVEX: Result := FilterOrderEVEX;
    iencMVEX: Result := FilterOrderMVEX;
  end;
end;

end.
