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

unit Zydis.Generator.Types;

interface

uses
  System.SysUtils, System.Generics.Collections, System.Generics.Defaults, Zydis.Generator.Base,
  Zydis.InstructionEditor, Zydis.InstructionFilters, Zydis.Enums.Filters;

{$SCOPEDENUMS ON}

type
  TZYDefinitionMapping = array[TZYInstructionEncoding] of TArray<Integer>;
  TZYDefinitionLists   = array[TZYInstructionEncoding] of TArray<TZYInstructionDefinition>;

  TZYDefinitionList = class sealed(TZYGeneratorTask)
  strict private
    FItems: TZYDefinitionLists;
    FItemCount: Integer;
    FUniqueItems: TZYDefinitionLists;
    FUniqueItemCount: Integer;
    FMapping: TZYDefinitionMapping;
  strict private
    procedure Initialize(Generator: TZYBaseGenerator; Editor: TZYInstructionEditor);
  public
    function Find(Definition: TZYInstructionDefinition): Integer; inline;
    function FindUnique(Definition: TZYInstructionDefinition): Integer; inline;
  public
    constructor Create(Generator: TZYBaseGenerator; Editor: TZYInstructionEditor);
  public
    property Items: TZYDefinitionLists read FItems;
    property ItemCount: Integer read FItemCount;
    property UniqueItems: TZYDefinitionLists read FUniqueItems;
    property UniqueItemCount: Integer read FUniqueItemCount;
    property Mapping: TZYDefinitionMapping read FMapping;
  end;

  TZYTreeItemType = (Invalid, Filter, Definition);
  PZYTreeItem = ^TZYTreeItem;
  TZYTreeItem = record
  public
    ItemType: TZYTreeItemType;
    Childs: TArray<TZYTreeItem>;
    IsDuplicate: Boolean;
    case Integer of
      0: (
        DefinitionId: Integer;
        DefinitionEncoding: TZYInstructionEncoding;
      );
      1: (
        FilterId: Integer;
        FilterClass: TZYInstructionFilterClass;
      );
  end;

  TZYFilterLists = array[TZYInstructionFilterClass] of TArray<PZYTreeItem>;

  TZYTreeSnapshot = class sealed(TZYGeneratorTask)
  strict private
    FGenerator: TZYBaseGenerator;
    FRoot: TZYTreeItem;
    FFilters: TZYFilterLists;
    FFilterCount: Integer;
    FFilterIds: array[TZYInstructionFilterClass] of Integer;
    FDefaultFilter: array[TZYInstructionFilterClass] of TZYTreeItem;
    FDefaultItem: TZYTreeItem;
  strict private
    function GetRoot: PZYTreeItem; inline;
  strict private
    class procedure TreeItemClear(var Target: TZYTreeItem); inline; static;
    class procedure TreeItemDuplicate(var Target: TZYTreeItem;
      const Source: TZYTreeItem); inline; static;
  strict private
    class function FindNegatedValueIndex(Node: TZYInstructionTreeNode): Integer; inline; static;
  strict private
    procedure CreateSnapshotRecursive(Definitions: TZYDefinitionList;
      Node: TZYInstructionTreeNode; var Item: TZYTreeItem);
    procedure CreateFilterListsRecursive(const Item: TZYTreeItem);
    procedure Initialize(Editor: TZYInstructionEditor; Definitions: TZYDefinitionList);
  public
    constructor Create(Generator: TZYBaseGenerator; Editor: TZYInstructionEditor;
      Definitions: TZYDefinitionList);
  public
    property Root: PZYTreeItem read GetRoot;
    property Filters: TZYFilterLists read FFilters;
    property FilterCount: Integer read FFilterCount;
  end;

  TZYUniqueOperandList = class sealed(TZYGeneratorTask)
  strict private
    FItems: TArray<TZYInstructionOperand>;
    FMapping: TZYDefinitionMapping;
  strict private
    class function NumberOfUsedOperands(Operands: TZYInstructionOperands): Integer; inline; static;
    class function GetOperand(Operands: TZYInstructionOperands;
      Index: Integer): TZYInstructionOperand; inline; static;
    class function IndexOfOperands(List: TList<TZYInstructionOperand>;
      Operands: TZYInstructionOperands): Integer; inline; static;
  strict private
    procedure Initialize(Generator: TZYBaseGenerator; Definitions: TZYDefinitionList); inline;
  public
    constructor Create(Generator: TZYBaseGenerator; Definitions: TZYDefinitionList);
  public
    property Items: TArray<TZYInstructionOperand> read FItems;
    property Mapping: TZYDefinitionMapping read FMapping;
  end;

  TZYUniqueDefinitionPropertyList<T> = class abstract(TZYGeneratorTask)
  strict private
    FItems: TArray<T>;
    FMapping: TZYDefinitionMapping;
  strict private
    procedure Initialize(Generator: TZYBaseGenerator; Definitions: TZYDefinitionList;
      const GetPropertyValue: TFunc<TZYInstructionDefinition, T>; HasAbsoluteOrder: Boolean;
      const EqualityComparer: IEqualityComparer<T>; const Comparer: IComparer<T>); inline;
  strict protected
    procedure SetDefaultValue(const Value: T); inline;
  public
    constructor Create(Generator: TZYBaseGenerator; Definitions: TZYDefinitionList;
      const GetPropertyValue: TFunc<TZYInstructionDefinition, T>;
      HasAbsoluteOrder: Boolean); overload;
    constructor Create(Generator: TZYBaseGenerator; Definitions: TZYDefinitionList;
      const GetPropertyValue: TFunc<TZYInstructionDefinition, T>; HasAbsoluteOrder: Boolean;
      const EqualityComparer: IEqualityComparer<T>; const Comparer: IComparer<T>); overload;
  public
    property Items: TArray<T> read FItems;
    property Mapping: TZYDefinitionMapping read FMapping;
  end;

  TZYUniqueDefinitionEnumPropertyList = class sealed(TZYUniqueDefinitionPropertyList<String>)
  public
    constructor Create(Generator: TZYBaseGenerator; Definitions: TZYDefinitionList;
      const GetPropertyValue: TFunc<TZYInstructionDefinition, String>); overload;
    constructor Create(Generator: TZYBaseGenerator; Definitions: TZYDefinitionList;
      const GetPropertyValue: TFunc<TZYInstructionDefinition, String>;
      const DefaultValue: String); overload;
  end;
  TZYGeneratorEnum = TZYUniqueDefinitionEnumPropertyList;

implementation

uses
  Utils.Comparator, Zydis.Enums;

{$REGION 'Class: TZYDefinitionList'}
constructor TZYDefinitionList.Create(Generator: TZYBaseGenerator; Editor: TZYInstructionEditor);
begin
  inherited Create;
  Initialize(Generator, Editor);
end;

function TZYDefinitionList.Find(Definition: TZYInstructionDefinition): Integer;
var
  I: Integer;
begin
  Result := -1;
  for I := Low(FItems[Definition.Encoding]) to High(FItems[Definition.Encoding]) do
  begin
    if (Definition = FItems[Definition.Encoding][I]) then
    begin
      Result := I;
      Break;
    end;
  end;
end;

function TZYDefinitionList.FindUnique(Definition: TZYInstructionDefinition): Integer;
var
  I: Integer;
begin
  Result := -1;
  for I := Low(FUniqueItems[Definition.Encoding]) to High(FUniqueItems[Definition.Encoding]) do
  begin
    if (Definition.HasEqualProperties(FUniqueItems[Definition.Encoding][I])) then
    begin
      Result := I;
      Break;
    end;
  end;
end;

procedure TZYDefinitionList.Initialize(Generator: TZYBaseGenerator; Editor: TZYInstructionEditor);
var
  I, J: Integer;
  N, U: array[TZYInstructionEncoding] of Integer;
  L: TList<TZYInstructionDefinition>;
  D: TZYInstructionDefinition;
  E: TZYInstructionEncoding;
  B: Boolean;
begin
  WorkStart(Generator, Editor.DefinitionCount * 2);

  FillChar(N, SizeOf(N), #0);
  FillChar(U, SizeOf(U), #0);
  for E := Low(TZYInstructionEncoding) to High(TZYInstructionEncoding) do
  begin
    SetLength(FItems[E], Editor.DefinitionCount);
    SetLength(FUniqueItems[E], Editor.DefinitionCount);
    SetLength(FMapping[E], Editor.DefinitionCount);
  end;

  L := TList<TZYInstructionDefinition>.Create;
  try
    // Create sorted definition-list to ensure deterministic output
    for I := 0 to Editor.DefinitionCount - 1 do
    begin
      D := Editor.Definitions[I];
      L.BinarySearch(D, J, TComparer<TZYInstructionDefinition>.Construct(
        function(const Left, Right: TZYInstructionDefinition): Integer
        begin
          Result := Left.CompareTo(Right);
          // TODO: Compare immediates to create an encoder-friendly order
        end));
      L.Insert(J, D);
      WorkStep(Generator);
    end;

    // Categorize definitions by encoding and eliminate duplicates for the unique-list
    for I := 0 to L.Count - 1 do
    begin
      D := L[I];
      E := L[I].Encoding;
      FItems[E][N[E]] := D;
      Inc(N[E]);

      B := false;
      for J := 0 to U[E] - 1 do
      begin
        if (D.HasEqualProperties(FUniqueItems[E][J])) then
        begin
          B := true;
          FMapping[E][I] := J;
          Break;
        end;
      end;
      if (not B) then
      begin
        FUniqueItems[E][U[E]] := D;
        FMapping[E][I] := U[E];
        Inc(U[E]);
      end;

      WorkStep(Generator);
    end;
  finally
    L.Free;
  end;

  I := 0;
  for E := Low(TZYInstructionEncoding) to High(TZYInstructionEncoding) do
  begin
    SetLength(FItems[E], N[E]);
    SetLength(FUniqueItems[E], U[E]);
    SetLength(FMapping[E], N[E]);
    Inc(I, U[E]);
  end;
  FItemCount := Editor.DefinitionCount;
  FUniqueItemCount := I;

  WorkEnd(Generator);
end;
{$ENDREGION}

{$REGION 'Class: TZYTreeSnapshot'}
constructor TZYTreeSnapshot.Create(Generator: TZYBaseGenerator; Editor: TZYInstructionEditor;
  Definitions: TZYDefinitionList);
begin
  inherited Create;
  FGenerator := Generator;
  Initialize(Editor, Definitions);
end;

procedure TZYTreeSnapshot.CreateFilterListsRecursive(const Item: TZYTreeItem);
var
  I: Integer;
begin
  FFilters[Item.FilterClass][FFilterIds[Item.FilterClass]] := @Item;
  Inc(FFilterIds[Item.FilterClass]);
  for I := Low(Item.Childs) to High(Item.Childs) do
  begin
    if (Item.Childs[I].ItemType = TZYTreeItemType.Filter) and (not Item.Childs[I].IsDuplicate) then
    begin
      Assert(not TZYInstructionFilterInfo.Info[Item.FilterClass].IsEditorOnly);
      CreateFilterListsRecursive(Item.Childs[I]);
    end;
  end;
  WorkStep(FGenerator);
end;

procedure TZYTreeSnapshot.CreateSnapshotRecursive(Definitions: TZYDefinitionList;
  Node: TZYInstructionTreeNode; var Item: TZYTreeItem);
var
  I, J, V: Integer;
  B: Boolean;
  N: TZYInstructionTreeNode;
  Filter: TZYInstructionFilter;
  Temp: TZYTreeItem;
begin
  TreeItemClear(Item);
  if (itnfIsLeafNode in Node.Flags) then
  begin
    Assert(Node.FilterClass = ifcInvalid);
    Assert(Node.DefinitionCount = 1);
    Item.ItemType := TZYTreeItemType.Definition;
    Item.DefinitionId := Definitions.FindUnique(Node.Definitions[0]);
    Item.DefinitionEncoding := Node.Definitions[0].Encoding;
    Assert(Item.DefinitionId >= 0);
  end else
  begin
    Inc(FFilterCount);
    Assert(Node.FilterClass <> ifcInvalid);
    Item.ItemType := TZYTreeItemType.Filter;
    Item.FilterClass := Node.FilterClass;
    B := false;
    Filter := TZYInstructionFilterInfo.Info[Node.FilterClass];
    if (Filter.IsCompactable) and (Node.ChildCount <= 2) then
    begin
      // Compress compactable filters
      B := Assigned(Node.Childs[Filter.IndexNegatedValueLo + Filter.CompactFilterValue]);
      if (not B) and (Assigned(Node.Childs[Filter.IndexValueLo + Filter.CompactFilterValue])) then
      begin
        B := true;
        for J := Filter.IndexValueLo to Filter.IndexValueHi do
        begin
          if (J <> Filter.IndexValueLo + Filter.CompactFilterValue) and
            (Assigned(Node.Childs[J])) then
          begin
            B := false;
            Break;
          end;
        end;
      end;
      if (B) then
      begin
        SetLength(Item.Childs, 2);
        N := Node.Childs[Filter.IndexValueLo + Filter.CompactFilterValue];
        if (Assigned(N)) then CreateSnapshotRecursive(Definitions, N, Item.Childs[0]);
        N := Node.Childs[Filter.IndexNegatedValueLo + Filter.CompactFilterValue];
        if (Assigned(N)) then CreateSnapshotRecursive(Definitions, N, Item.Childs[1]);
        Filter := TZYInstructionFilterInfo.Info[Filter.CompactFilterClass];
        Assert(Filter.NumberOfValues = 2);
        Item.FilterClass := Filter.FilterClass;
      end;
    end;
    Item.FilterId := FFilterIds[Item.FilterClass];
    Inc(FFilterIds[Item.FilterClass]);
    if (not B) then
    begin
      SetLength(Item.Childs, Filter.NumberOfValues);
      V := FindNegatedValueIndex(Node);
      if (V >= 0) then
      begin
        // Resolve negated values
        TreeItemClear(Temp);
        if (itnfIsLeafNode in Node.Childs[V].Flags) or (Node.Childs[V].ChildCount > 0) or
          (Node.Childs[V].FilterClass in [ifcVEX, ifcEMVEX]) then
        begin
          CreateSnapshotRecursive(Definitions, Node.Childs[V], Temp);
        end;
        for I := Filter.IndexValueLo to Filter.IndexValueHi do
        begin
          if (I = V - Integer(Filter.NumberOfValues)) then
          begin
            if (Assigned(Node.Childs[I])) then
            begin
              CreateSnapshotRecursive(
                Definitions, Node.Childs[I], Item.Childs[I - Filter.IndexValueLo]);
            end else
            begin
              TreeItemClear(Item.Childs[I - Filter.IndexValueLo]);
            end;
          end else
          begin
            if (Temp.IsDuplicate) then
            begin
              TreeItemDuplicate(Item.Childs[I - Filter.IndexValueLo], Temp);
            end else
            begin
              Item.Childs[I - Filter.IndexValueLo] := Temp;
              Temp.IsDuplicate := true;
            end;
          end;
        end;
      end else
      begin
        for I := Low(Item.Childs) to High(Item.Childs) do
        begin
          TreeItemClear(Item.Childs[I]);
          if (not Assigned(Node.Childs[Filter.IndexValueLo + I])) then
          begin
            Continue;
          end;
          if (itnfIsLeafNode in Node.Childs[Filter.IndexValueLo + I].Flags) or
            (Node.Childs[Filter.IndexValueLo + I].ChildCount > 0) or
            (Node.Childs[Filter.IndexValueLo + I].FilterClass in [ifcVEX, ifcEMVEX]) then
          begin
            CreateSnapshotRecursive(
              Definitions, Node.Childs[Filter.IndexValueLo + I], Item.Childs[I]);
          end;
        end;
      end;
    end;
    WorkStep(FGenerator);
  end;
end;

class function TZYTreeSnapshot.FindNegatedValueIndex(Node: TZYInstructionTreeNode): Integer;
var
  Filter: TZYInstructionFilter;
  I: Integer;
begin;
  Filter := TZYInstructionFilterInfo.Info[Node.FilterClass];
  Assert(not Filter.IsEditorOnly);
  Result := -1;
  if (not Filter.SupportsNegatedValues) then
  begin
    Exit;
  end;
  for I := Filter.IndexNegatedValueLo to Filter.IndexNegatedValueHi do
  begin
    if (Assigned(Node.Childs[I])) then
    begin
      Result := I;
      Exit;
    end;
  end;
end;

function TZYTreeSnapshot.GetRoot: PZYTreeItem;
begin
  Result := @FRoot;
end;

procedure TZYTreeSnapshot.Initialize(Editor: TZYInstructionEditor;
  Definitions: TZYDefinitionList);
var
  C: TZYInstructionFilterClass;
  F: TZYInstructionFilter;
  I: Integer;
begin
  WorkStart(FGenerator, Editor.FilterCountTotal * 2);
  CreateSnapshotRecursive(Definitions, Editor.RootNode, FRoot);

  // Initialize 2-byte VEX filter
  // MAP0
  TreeItemDuplicate(FRoot.Childs[$C5].Childs[$01], FRoot.Childs[$C4].Childs[$01]);
  // 0x0F
  TreeItemDuplicate(FRoot.Childs[$C5].Childs[$02], FRoot.Childs[$C4].Childs[$02]);
  // 0x66 0x0F
  TreeItemDuplicate(FRoot.Childs[$C5].Childs[$06], FRoot.Childs[$C4].Childs[$06]);
  // 0xF3 0x0F
  TreeItemDuplicate(FRoot.Childs[$C5].Childs[$0A], FRoot.Childs[$C4].Childs[$0A]);
  // 0xF2 0x0F
  TreeItemDuplicate(FRoot.Childs[$C5].Childs[$0E], FRoot.Childs[$C4].Childs[$0E]);

  for C := Low(TZYInstructionFilterClass) to High(TZYInstructionFilterClass) do
  begin
    SetLength(FFilters[C], FFilterIds[C]);
  end;
  FillChar(FFilterIds, SizeOf(FFilterIds), #0);

  CreateFilterListsRecursive(FRoot);

  // Create empty defaults for unused filters
  TreeItemClear(FDefaultItem);
  for C := Low(TZYInstructionFilterClass) to High(TZYInstructionFilterClass) do
  begin
    if (Length(FFilters[C]) = 0) then
    begin
      F := TZYInstructionFilterInfo.Info[C];
      FDefaultFilter[C] := FDefaultItem;
      FDefaultFilter[C].ItemType := TZYTreeItemType.Filter;
      FDefaultFilter[C].FilterClass := C;
      SetLength(FDefaultFilter[C].Childs, F.NumberOfValues);
      for I := 0 to Integer(F.NumberOfValues) - 1 do
      begin
        FDefaultFilter[C].Childs[I] := FDefaultItem;
      end;
      SetLength(FFilters[C], 1);
      FFilters[C][0] := @FDefaultFilter[C];
    end;
  end;

  // Link REX2 nodes to maps
  Assert(Length(FFilters[ifcREX2]) = 1);
  TreeItemDuplicate(FRoot.Childs[$D5].Childs[$01], FRoot);
  TreeItemDuplicate(FRoot.Childs[$D5].Childs[$02], FRoot.Childs[$0F]);

  WorkEnd(FGenerator);
end;

class procedure TZYTreeSnapshot.TreeItemClear(var Target: TZYTreeItem);
begin
  Target.ItemType := TZYTreeItemType.Invalid;
  Target.IsDuplicate := false;
  SetLength(Target.Childs, 0);
end;

class procedure TZYTreeSnapshot.TreeItemDuplicate(var Target: TZYTreeItem;
  const Source: TZYTreeItem);
begin
  Target.ItemType := Source.ItemType;
  Target.DefinitionEncoding := Source.DefinitionEncoding;
  Target.DefinitionId := Source.DefinitionId;
  Target.FilterClass := Source.FilterClass;
  Target.FilterId := Source.FilterId;
  Target.IsDuplicate := true;
end;
{$ENDREGION}

{$REGION 'Class: TZYUniqueOperandList'}
constructor TZYUniqueOperandList.Create(Generator: TZYBaseGenerator;
  Definitions: TZYDefinitionList);
begin
  inherited Create;
  Initialize(Generator, Definitions);
end;

class function TZYUniqueOperandList.GetOperand(Operands: TZYInstructionOperands;
  Index: Integer): TZYInstructionOperand;
begin

  if (Index < Operands.NumberOfUsedOperands) then
  begin
    Exit(Operands.Items[Index]);
  end;
  if (Index = Operands.NumberOfUsedOperands + 0) then
  begin
    if (Operands.Definition.AffectedFlags.AutomaticOperand0.OperandType = optUnused) then
    begin
      Inc(Index);
    end else
    begin
      Assert(Operands.Definition.AffectedFlags.AutomaticOperand0.OperandType <> optUnused);
      Exit(Operands.Definition.AffectedFlags.AutomaticOperand0);
    end;
  end;
  if (Index = Operands.NumberOfUsedOperands + 1) then
  begin
    Assert(Operands.Definition.AffectedFlags.AutomaticOperand1.OperandType <> optUnused);
    Exit(Operands.Definition.AffectedFlags.AutomaticOperand1);
  end;

  // This wrapper function takes care of the automatically generated FLAGS-register operand
  {if (Index = Operands.NumberOfUsedOperands) and
    (Operands.Definition.AffectedFlags.AutomaticOperand.OperandType <> optUnused) then
  begin
    Result := Operands.Definition.AffectedFlags.AutomaticOperand;
  end else
  begin
    Result := Operands.Items[Index];
  end;}
end;

class function TZYUniqueOperandList.IndexOfOperands(List: TList<TZYInstructionOperand>;
  Operands: TZYInstructionOperands): Integer;
var
  I, J, V: Integer;
  B: Boolean;
begin
  Result := -1;
  I := 0;
  while I < List.Count do
  begin
    if (I + NumberOfUsedOperands(Operands) > List.Count) then
    begin
      Break;
    end;
    B := true;
    V := I;
    for J := 0 to NumberOfUsedOperands(Operands) - 1 do
    begin
      B := B and List[I].Equals(GetOperand(Operands, J));
      Inc(I);
      if (not B) then
      begin
        Break;
      end;
    end;
    if (B) then
    begin
      Result := I - NumberOfUsedOperands(Operands);
      Break;
    end else
    begin
      I := V + 1;
    end;
  end;
end;

procedure TZYUniqueOperandList.Initialize(Generator: TZYBaseGenerator;
  Definitions: TZYDefinitionList);
var
  L: TList<TZYInstructionOperand>;
  I, J, K, V: Integer;
  E: TZYInstructionEncoding;
  O: TZYInstructionOperands;
begin
  WorkStart(Generator, Definitions.UniqueItemCount);
  L := TList<TZYInstructionOperand>.Create;
  try
    for I := TZYInstructionOperands.Count downto 1 do
    begin
      for E := Low(TZYInstructionEncoding) to High(TZYInstructionEncoding) do
      begin
        SetLength(FMapping[E], Length(Definitions.UniqueItems[E]));
        for J := Low(Definitions.UniqueItems[E]) to High(Definitions.UniqueItems[E]) do
        begin
          O := Definitions.UniqueItems[E][J].Operands;
          if (NumberOfUsedOperands(O) <> I) then
          begin
            Continue;
          end;
          V := IndexOfOperands(L, O);
          if (V < 0) then
          begin
            V := L.Count;
            for K := 0 to NumberOfUsedOperands(O) - 1 do
            begin
              L.Add(GetOperand(O, K));
            end;
          end;
          FMapping[E][J] := V;
          WorkStep(Generator);
        end;
      end;
    end;
    FItems := L.ToArray;
  finally
    L.Free;
  end;
  WorkEnd(Generator);
end;

class function TZYUniqueOperandList.NumberOfUsedOperands(Operands: TZYInstructionOperands): Integer;
begin
  // This wrapper function takes care of the automatically generated FLAGS-register operand
  Result := Operands.NumberOfUsedOperands;
  if (Operands.Definition.AffectedFlags.AutomaticOperand0.OperandType <> optUnused) then
  begin
    Inc(Result);
  end;
  if (Operands.Definition.AffectedFlags.AutomaticOperand1.OperandType <> optUnused) then
  begin
    Inc(Result);
  end;
end;
{$ENDREGION}

{$REGION 'Class: TZYUniqueDefinitionPropertyList'}
constructor TZYUniqueDefinitionPropertyList<T>.Create(Generator: TZYBaseGenerator;
  Definitions: TZYDefinitionList; const GetPropertyValue: TFunc<TZYInstructionDefinition, T>;
  HasAbsoluteOrder: Boolean; const EqualityComparer: IEqualityComparer<T>;
  const Comparer: IComparer<T>);
begin
  inherited Create;
  Initialize(
    Generator, Definitions, GetPropertyValue, HasAbsoluteOrder, EqualityComparer, Comparer);
end;

constructor TZYUniqueDefinitionPropertyList<T>.Create(Generator: TZYBaseGenerator;
  Definitions: TZYDefinitionList; const GetPropertyValue: TFunc<TZYInstructionDefinition, T>;
  HasAbsoluteOrder: Boolean);
begin
  Create(Generator, Definitions, GetPropertyValue, HasAbsoluteOrder, TEqualityComparer<T>.Default,
    TComparer<T>.Default);
end;

procedure TZYUniqueDefinitionPropertyList<T>.Initialize(Generator: TZYBaseGenerator;
  Definitions: TZYDefinitionList; const GetPropertyValue: TFunc<TZYInstructionDefinition, T>;
  HasAbsoluteOrder: Boolean; const EqualityComparer: IEqualityComparer<T>;
  const Comparer: IComparer<T>);
var
  I, J, N, C, K: Integer;
  E: TZYInstructionEncoding;
  D: TZYInstructionDefinition;
  V: T;
  B: Boolean;
begin
  N := Definitions.UniqueItemCount;
  if (HasAbsoluteOrder) then
  begin
    N := N * 2;
  end;
  WorkStart(Generator, N);
  N := Length(FItems);
  Assert((N = 0) or (N = 1));
  SetLength(FItems, Definitions.UniqueItemCount + N);
  C := N;
  for E := Low(TZYInstructionEncoding) to High(TZYInstructionEncoding) do
  begin
    SetLength(FMapping[E], Length(Definitions.UniqueItems[E]));
    for I := Low(Definitions.UniqueItems[E]) to High(Definitions.UniqueItems[E]) do
    begin
      D := Definitions.UniqueItems[E][I];
      V := GetPropertyValue(D);
      if (HasAbsoluteOrder) then
      begin
        // Use binary search to get the insert position and add the items, if it's not already in
        // the list
        if (N = 1) and (
          (EqualityComparer.Equals(V, Default(T))) or
          (EqualityComparer.Equals(V, FItems[0]))) then
        begin
          Continue;
        end;
        if (not TArray.BinarySearch<T>(FItems, V, J, Comparer, N, C - N)) then
        begin
          for K := C downto J + 1 do
          begin
            FItems[K] := FItems[K - 1];
          end;
          FItems[J] := V;
          Inc(C);
        end;
      end else
      begin
        if (N = 1) and (
          (EqualityComparer.Equals(V, Default(T))) or
          (EqualityComparer.Equals(V, FItems[0]))) then
        begin
          FMapping[E][I] := 0;
          Continue;
        end;
        // Use a basic linear search and add the item, if it's not already in the list
        B := false;
        for J := Low(FItems) to High(FItems) do
        begin
          if (EqualityComparer.Equals(V, Items[J])) then
          begin
            B := true;
            FMapping[E][I] := J;
            Break;
          end;
        end;
        if (not B) then
        begin
          FItems[C] := V;
          FMapping[E][I] := C;
          Inc(C);
        end;
      end;
      WorkStep(Generator);
    end;
  end;
  SetLength(FItems, C);
  if (HasAbsoluteOrder) then
  begin
    for E := Low(TZYInstructionEncoding) to High(TZYInstructionEncoding) do
    begin
      for I := Low(Definitions.UniqueItems[E]) to High(Definitions.UniqueItems[E]) do
      begin
        D := Definitions.UniqueItems[E][I];
        V := GetPropertyValue(D);
        if (N = 1) and (
          (EqualityComparer.Equals(V, Default(T))) or
          (EqualityComparer.Equals(V, FItems[0]))) then
        begin
          FMapping[E][I] := 0;
          Continue;
        end;
{$IFOPT C+}
        Assert(TArray.BinarySearch<T>(FItems, V, J, Comparer, N, Length(Items) - N));
{$ELSE}
        TArray.BinarySearch<T>(FItems, V, J, Comparer, N, Length(Items) - N);
{$ENDIF}
        FMapping[E][I] := J;
        WorkStep(Generator);
      end;
    end;
  end;
  WorkEnd(Generator);
end;

procedure TZYUniqueDefinitionPropertyList<T>.SetDefaultValue(const Value: T);
begin
  SetLength(FItems, 1);
  FItems[0] := Value;
end;
{$ENDREGION}

{$REGION 'Class: TZYUniqueDefinitionEnumPropertyList'}
constructor TZYUniqueDefinitionEnumPropertyList.Create(Generator: TZYBaseGenerator;
  Definitions: TZYDefinitionList;
  const GetPropertyValue: TFunc<TZYInstructionDefinition, String>);
begin
  inherited Create(Generator, Definitions, GetPropertyValue, true);
end;

constructor TZYUniqueDefinitionEnumPropertyList.Create(Generator: TZYBaseGenerator;
  Definitions: TZYDefinitionList;
  const GetPropertyValue: TFunc<TZYInstructionDefinition, String>; const DefaultValue: String);
begin
  SetDefaultValue(DefaultValue);
  inherited Create(Generator, Definitions, GetPropertyValue, true);
end;
{$ENDREGION}

end.
