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

unit Zydis.InstructionEditor;

interface

uses
  System.SysUtils, System.Classes, System.Generics.Collections, System.JSON, Utils.JSON,
  Zydis.Enums, Zydis.Enums.Filters, Zydis.InstructionFilters;

type
  TZYBasePersistent = class abstract(TPersistent)
  strict private
    FUpdateCount: Integer;
    FUpdateRequired: Boolean;
  protected
    procedure Changed; virtual; abstract;
  strict protected
    constructor Create;
  public
    procedure BeginUpdate; inline;
    procedure Update; inline;
    procedure EndUpdate; inline;
  end;

  TZYJSONAPersistent = class abstract(TZYBasePersistent)
  public
    procedure LoadFromJSON(const JSON: IJSONArrayReader); virtual; abstract;
    procedure SaveToJSON(const JSON: IJSONArrayWriter); virtual; abstract;
  end;

  TZYJSONOPersistent = class abstract(TZYBasePersistent)
  public
    procedure LoadFromJSON(const JSON: IJSONObjectReader); virtual; abstract;
    procedure SaveToJSON(const JSON: IJSONObjectWriter); virtual; abstract;
  end;

  TZYLinkedPersistent<T: TZYBasePersistent> = class abstract(TZYBasePersistent)
  strict private
    FParent: T;
  protected
    procedure Changed; override;
  strict protected
    constructor Create(Parent: T);
  strict protected
    property Parent: T read FParent;
  end;

  TZYLinkedJSONAPersistent<T: TZYBasePersistent> = class abstract(TZYLinkedPersistent<T>)
  public
    procedure LoadFromJSON(const JSON: IJSONArrayReader); virtual; abstract;
    procedure SaveToJSON(const JSON: IJSONArrayWriter); virtual; abstract;
  end;

  TZYLinkedJSONOPersistent<T: TZYBasePersistent> = class abstract(TZYLinkedPersistent<T>)
  public
    procedure LoadFromJSON(const JSON: IJSONObjectReader); virtual; abstract;
    procedure SaveToJSON(const JSON: IJSONObjectWriter); virtual; abstract;
  end;

  TZYInstructionDefinition = class;

  TZYDefinitionComposite = class abstract(TZYBasePersistent)
  strict private
    FDefinition: TZYInstructionDefinition;
  protected
    procedure Changed; override;
  strict protected
    constructor Create(Definition: TZYInstructionDefinition);
  public
    property Definition: TZYInstructionDefinition read FDefinition;
  end;

  TZYJSONADefinitionComposite = class abstract(TZYDefinitionComposite)
  public
    procedure LoadFromJSON(const JSON: IJSONArrayReader); virtual; abstract;
    procedure SaveToJSON(const JSON: IJSONArrayWriter); virtual; abstract;
  end;

  TZYJSONODefinitionComposite = class abstract(TZYDefinitionComposite)
  public
    procedure LoadFromJSON(const JSON: IJSONObjectReader); virtual; abstract;
    procedure SaveToJSON(const JSON: IJSONObjectWriter); virtual; abstract;
  end;

  TZYInstructionEditor = class;

  TZYInstructionTreeNodeFlag = (
    {**
     * This is the root node.
     *}
    itnfIsRootNode,
    {**
     * This node is a leaf node that contains instruction-definitions.
     *}
    itnfIsLeafNode,
    {**
     * The node is a static node that should not get optimized away in any case.
     * Without this flag set, the optimization routine will unlink any node with only its
     * placeholder-slot used.
     *}
    itnfIsStaticNode
  );
  TZYInstructionTreeNodeFlags = set of TZYInstructionTreeNodeFlag;

  TZYInstructionTreeNodeConflict = (
    {**
     * A conflict caused by one or more child-nodes.
     *}
    itncInheritedConflict,
    {**
     * TODO:
     *}
    itncPlaceholderConflict,
    {**
     * TODO:
     *}
    itncNegatedValueConflict,
    {**
     * A conflict caused by one ore more assigned instruction-definitions.
     *}
    itncDefinitionConflict,
    {**
     * The node has more than one instruction-definition assigned.
     *}
    itncDefinitionCount
  );
  TZYInstructionTreeNodeConflicts = set of TZYInstructionTreeNodeConflict;

  TZYInstructionTreeNode = class sealed(TZYBasePersistent)
  strict private
    FEditor: TZYInstructionEditor;
    FParent: TZYInstructionTreeNode;
    FFlags: TZYInstructionTreeNodeFlags;
    FFilterClass: TZYInstructionFilterClass;
    FChilds: TArray<TZYInstructionTreeNode>;
    FChildCount: Integer;
    FChildCapacity: Integer;
    FDefinitions: TList<TZYInstructionDefinition>;
    FConflicts: TZYInstructionTreeNodeConflicts;
    FInheritedConflicts: Integer;
    FDefinitionConflicts: Integer;
    FData: Pointer;
  strict private
    function GetChild(Index: Integer): TZYInstructionTreeNode; inline;
    function GetDefinition(Index: Integer): TZYInstructionDefinition; inline;
    function GetDefinitionCount: Integer; inline;
  strict private
    procedure SetParent(const Value: TZYInstructionTreeNode);
    procedure SetConflicts(const Value: TZYInstructionTreeNodeConflicts); inline;
  strict private
    procedure IncInheritedConflictCount; inline;
    procedure DecInheritedConflictCount; inline;
  private
    procedure SetChildItem(Index: Integer; const Value: TZYInstructionTreeNode);
  private
    procedure IncDefinitionConflictCount; inline;
    procedure DecDefinitionConflictCount; inline;
  private
    procedure InsertDefinition(Definition: TZYInstructionDefinition);
    procedure RemoveDefinition(Definition: TZYInstructionDefinition);
  private
    procedure CreateChildNodeAtIndex(Index: Integer; FilterClass: TZYInstructionFilterClass;
      IsRootNode: Boolean = false; IsStaticNode: Boolean = false); inline;
  strict protected
    procedure Changed; override;
  protected
    constructor Create(Editor: TZYInstructionEditor; FilterClass: TZYInstructionFilterClass;
      IsRootNode: Boolean = false; IsStaticNode: Boolean = false);
  public
    function HasConflicts: Boolean; inline;
    function IndexOf(ChildNode: TZYInstructionTreeNode): Integer;
  public
    destructor Destroy; override;
  public
    property Editor: TZYInstructionEditor read FEditor;
    property Parent: TZYInstructionTreeNode read FParent;
    property Childs[Index: Integer]: TZYInstructionTreeNode read GetChild;
    property ChildCount: Integer read FChildCount;
    property ChildCapacity: Integer read FChildCapacity;
    property Definitions[Index: Integer]: TZYInstructionDefinition read GetDefinition;
    property DefinitionCount: Integer read GetDefinitionCount;
    property Data: Pointer read FData write FData;
  published
    property Flags: TZYInstructionTreeNodeFlags read FFlags;
    property FilterClass: TZYInstructionFilterClass read FFilterClass;
    property Conflicts: TZYInstructionTreeNodeConflicts read FConflicts default [];
    property InheritedConflicts: Integer read FInheritedConflicts default 0;
    property DefinitionConflicts: Integer read FDefinitionConflicts default 0;
  end;

  EZYDefinitionPropertyException = class(Exception);

  TZYInstructionFilters = class sealed(TZYJSONODefinitionComposite)
  strict private
    FForceModrmReg: Boolean;
    FForceModrmRm: Boolean;
  strict private
    function GetMode: TZYFilterMode; inline;
    function GetModrmMod: TZYFilterModrmMod; inline;
    function GetModrmReg: TZYFilterModrmReg; inline;
    function GetModrmRm: TZYFilterModrmRm; inline;
    function GetMandatoryPrefix: TZYFilterMandatoryPrefix; inline;
    function GetOperandSize: TZYFilterOperandSize; inline;
    function GetAddressSize: TZYFilterAddressSize; inline;
    function GetVectorLength: TZYFilterVectorLength; inline;
    function GetRexW: TZYFilterRexW; inline;
    function GetRexB: TZYFilterRexB; inline;
    function GetEvexB: TZYFilterEvexB; inline;
    function GetMvexE: TZYFilterMvexE; inline;

    function GetModeAMD: TZYFilterBoolean; inline;
    function GetModeKNC: TZYFilterBoolean; inline;
    function GetModeMPX: TZYFilterBoolean; inline;
    function GetModeCET: TZYFilterBoolean; inline;
    function GetModeLZCNT: TZYFilterBoolean; inline;
    function GetModeTZCNT: TZYFilterBoolean; inline;
  strict private
    procedure SetMode(Value: TZYFilterMode); inline;
    procedure SetModrmMod(Value: TZYFilterModrmMod); inline;
    procedure SetModrmReg(Value: TZYFilterModrmReg); inline;
    procedure SetModrmRm(Value: TZYFilterModrmRm); inline;
    procedure SetMandatoryPrefix(Value: TZYFilterMandatoryPrefix); inline;
    procedure SetOperandSize(Value: TZYFilterOperandSize); inline;
    procedure SetAddressSize(Value: TZYFilterAddressSize); inline;
    procedure SetVectorLength(Value: TZYFilterVectorLength); inline;
    procedure SetRexW(Value: TZYFilterRexW); inline;
    procedure SetRexB(Value: TZYFilterRexB); inline;
    procedure SetEvexB(Value: TZYFilterEvexB); inline;
    procedure SetMvexE(Value: TZYFilterMvexE); inline;

    procedure SetModeAMD(Value: TZYFilterBoolean); inline;
    procedure SetModeKNC(Value: TZYFilterBoolean); inline;
    procedure SetModeMPX(Value: TZYFilterBoolean); inline;
    procedure SetModeCET(Value: TZYFilterBoolean); inline;
    procedure SetModeLZCNT(Value: TZYFilterBoolean); inline;
    procedure SetModeTZCNT(Value: TZYFilterBoolean); inline;

    procedure SetForceModrmReg(Value: Boolean); inline;
    procedure SetForceModrmRm(Value: Boolean); inline;
  protected
    procedure Changed; override;
  protected
    procedure AssignTo(Dest: TPersistent); override;
  protected
    constructor Create(Definition: TZYInstructionDefinition);
  public
    procedure LoadFromJSON(const JSON: IJSONObjectReader); override;
    procedure SaveToJSON(const JSON: IJSONObjectWriter); override;
  public
    function Equals(Obj: TObject): Boolean; override;
  published
    property Mode: TZYFilterMode read GetMode write SetMode default modePlaceholder;
    property ModrmMod: TZYFilterModrmMod read GetModrmMod write SetModrmMod default mdPlaceholder;
    property ModrmReg: TZYFilterModrmReg read GetModrmReg write SetModrmReg default rgPlaceholder;
    property ModrmRm: TZYFilterModrmRm read GetModrmRm write SetModrmRm default rmPlaceholder;
    property MandatoryPrefix: TZYFilterMandatoryPrefix read GetMandatoryPrefix
      write SetMandatoryPrefix default mpPlaceholder;
    property OperandSize: TZYFilterOperandSize read GetOperandSize write SetOperandSize
      default osPlaceholder;
    property AddressSize: TZYFilterAddressSize read GetAddressSize write SetAddressSize
      default asPlaceholder;
    property VectorLength: TZYFilterVectorLength read GetVectorLength write SetVectorLength
      default vlPlaceholder;
    property RexW: TZYFilterRexW read GetRexW write SetRexW default rwPlaceholder;
    property RexB: TZYFilterRexB read GetRexB write SetRexB default rbPlaceholder;
    property EvexB: TZYFilterEvexB read GetEvexB write SetEvexB default ebPlaceholder;
    property MvexE: TZYFilterMvexE read GetMvexE write SetMvexE default mePlaceholder;

    property ModeAMD: TZYFilterBoolean read GetModeAMD write SetModeAMD default fbPlaceholder;
    property ModeKNC: TZYFilterBoolean read GetModeKNC write SetModeKNC default fbPlaceholder;
    property ModeMPX: TZYFilterBoolean read GetModeMPX write SetModeMPX default fbPlaceholder;
    property ModeCET: TZYFilterBoolean read GetModeCET write SetModeCET default fbPlaceholder;
    property ModeLZCNT: TZYFilterBoolean read GetModeLZCNT write SetModeLZCNT default fbPlaceholder;
    property ModeTZCNT: TZYFilterBoolean read GetModeTZCNT write SetModeTZCNT default fbPlaceholder;

    property ForceModrmReg: Boolean read FForceModrmReg write SetForceModrmReg default false; // TODO: Move! Does not fit here. + rename (extends opcode)
    property ForceModrmRm: Boolean read FForceModrmRm write SetForceModrmRm default false;    // TODO: Move! Does not fit here. + rename (extends opcode)
  end;

  TZYInstructionOperand = class;

  TZYInstructionOperands = class sealed(TZYJSONADefinitionComposite)
  strict private const
    MAX_OPERAND_COUNT = 10;
  strict private
    FOperands: array[0..MAX_OPERAND_COUNT - 1] of TZYInstructionOperand;
    FNumberOfUsedOperands: Integer;
  strict private
    class function GetOperandCount: Integer; static; inline;
    function GetOperand(Index: Integer): TZYInstructionOperand; inline;
  strict private
    procedure UpdateNumberOfUsedOperands; inline;
  protected
    procedure AssignTo(Dest: TPersistent); override;
  protected
    procedure Changed; override;
  protected
    constructor Create(Definition: TZYInstructionDefinition);
  public
    procedure LoadFromJSON(const JSON: IJSONArrayReader); override;
    procedure SaveToJSON(const JSON: IJSONArrayWriter); override;
  public
    function Equals(Obj: TObject): Boolean; override;
  public
    destructor Destroy; override;
  public
    class property Count: Integer read GetOperandCount;
    property Items[Index: Integer]: TZYInstructionOperand read GetOperand;
    property NumberOfUsedOperands: Integer read FNumberOfUsedOperands;
  published
    property OperandA: TZYInstructionOperand index 0 read GetOperand;
    property OperandB: TZYInstructionOperand index 1 read GetOperand;
    property OperandC: TZYInstructionOperand index 2 read GetOperand;
    property OperandD: TZYInstructionOperand index 3 read GetOperand;
    property OperandE: TZYInstructionOperand index 4 read GetOperand;
    property OperandF: TZYInstructionOperand index 5 read GetOperand;
    property OperandG: TZYInstructionOperand index 6 read GetOperand;
    property OperandH: TZYInstructionOperand index 7 read GetOperand;
    property OperandI: TZYInstructionOperand index 8 read GetOperand;
    property OperandJ: TZYInstructionOperand index 9 read GetOperand;
  end;

  TZYSemanticOperandWidth = type Cardinal;

  TZYInstructionOperand = class sealed(TZYLinkedJSONOPersistent<TZYInstructionOperands>)
  strict private
    FIndex: Integer;
    FAction: TZYOperandAction;
    FOperandType: TZYOperandType;
    FEncoding: TZYOperandEncoding;
    FRegister: TZYRegister;
    FMemorySegment: TZYSegmentRegister;
    FMemoryBase: TZYBaseRegister;
    FElementType: TZYElementType;
    FScaleFactor: TZYScaleFactor;
    FWidth: array[1..3] of TZYSemanticOperandWidth;
    FVisible: Boolean;
  strict private
    function GetWidth(Index: Integer): TZYSemanticOperandWidth; inline;
    function GetVisibility: TZYOperandVisibility; inline;
  strict private
    procedure SetAction(const Value: TZYOperandAction); inline;
    procedure SetOperandType(const Value: TZYOperandType); inline;
    procedure SetEncoding(const Value: TZYOperandEncoding); inline;
    procedure SetRegister(const Value: TZYRegister); inline;
    procedure SetMemorySegment(const Value: TZYSegmentRegister); inline;
    procedure SetMemoryBase(const Value: TZYBaseRegister); inline;
    procedure SetElementType(const Value: TZYElementType); inline;
    procedure SetScaleFactor(const Value: TZYScaleFactor); inline;
    procedure SetWidth(Index: Integer; const Value: TZYSemanticOperandWidth); inline;
    procedure SetVisible(const Value: Boolean); inline;
  protected
    procedure AssignTo(Dest: TPersistent); override;
  protected
    procedure Changed; override;
  protected
    constructor Create(Parent: TZYInstructionOperands; Index: Integer);
  public
    procedure LoadFromJSON(const JSON: IJSONObjectReader); override;
    procedure SaveToJSON(const JSON: IJSONObjectWriter); override;
  public
    function Equals(Obj: TObject): Boolean; override;
  public
    destructor Destroy; override;
  published
    property Index: Integer read FIndex;
    property Action: TZYOperandAction read FAction write SetAction default opaRead;
    property OperandType: TZYOperandType read FOperandType write SetOperandType default optUnused;
    property Encoding: TZYOperandEncoding read FEncoding write SetEncoding default opeNone;
    property Register: TZYRegister read FRegister write SetRegister default regNone;
    property MemorySegment: TZYSegmentRegister read FMemorySegment write SetMemorySegment
      default sregNone;
    property MemoryBase: TZYBaseRegister read FMemoryBase write SetMemoryBase default bregNone;
    property ElementType: TZYElementType read FElementType write SetElementType default emtInvalid;
    property ScaleFactor: TZYScaleFactor read FScaleFactor write SetScaleFactor default sfStatic;
    property Width16: TZYSemanticOperandWidth index 1 read GetWidth write SetWidth default 0;
    property Width32: TZYSemanticOperandWidth index 2 read GetWidth write SetWidth default 0;
    property Width64: TZYSemanticOperandWidth index 3 read GetWidth write SetWidth default 0;
    property Visible: Boolean read FVisible write SetVisible default true;
    property Visibility: TZYOperandVisibility read GetVisibility;
  end;

  TZYInstructionMetaInfo = class sealed(TZYJSONODefinitionComposite)
  strict private
    FCategory: String;
    FExtension: String;
    FISASet: String;
  strict private
    procedure SetCategory(const Value: String); inline;
    procedure SetExtension(const Value: String); inline;
    procedure SetISASet(const Value: String); inline;
  protected
    procedure AssignTo(Dest: TPersistent); override;
  protected
    constructor Create(Definition: TZYInstructionDefinition);
  public
    procedure LoadFromJSON(const JSON: IJSONObjectReader); override;
    procedure SaveToJSON(const JSON: IJSONObjectWriter); override;
  public
    function Equals(Obj: TObject): Boolean; override;
  public
    destructor Destroy; override;
  published
    property Category: String read FCategory write SetCategory;
    property Extension: String read FExtension write SetExtension;
    property ISASet: String read FISASet write SetISASet;
  end;

  TZYInstructionFlagsInfo = class sealed(TZYJSONODefinitionComposite)
  strict private
    FAccess: TZYFlagsAccess;
    FFlags: array[0..20] of TZYFlagOperation;
    FManagedOperand: TZYInstructionOperand;
  strict private
    function GetFlagOperation(Index: Integer): TZYFlagOperation; inline;
    function GetFlagCount: Integer; inline;
  strict private
    procedure SetAccess(Value: TZYFlagsAccess); inline;
    procedure SetFlagOperation(Index: Integer; Value: TZYFlagOperation); inline;
  strict private
    procedure UpdateManagedOperand; inline;
  protected
    procedure AssignTo(Dest: TPersistent); override;
  protected
    procedure Changed; override;
  protected
    constructor Create(Definition: TZYInstructionDefinition);
  public
    procedure LoadFromJSON(const JSON: IJSONObjectReader); override;
    procedure SaveToJSON(const JSON: IJSONObjectWriter); override;
  public
    function Equals(Obj: TObject): Boolean; override;
  public
    destructor Destroy; override;
  public
    property AutomaticOperand: TZYInstructionOperand read FManagedOperand;
    property Flags[Index: Integer]: TZYFlagOperation read GetFlagOperation;
    property Count: Integer read GetFlagCount;
  published
    property Access: TZYFlagsAccess read FAccess write SetAccess default faReadOnly;
    property CF  : TZYFlagOperation index  0 read GetFlagOperation write SetFlagOperation
      default foNone;
    property PF  : TZYFlagOperation index  1 read GetFlagOperation write SetFlagOperation
      default foNone;
    property AF  : TZYFlagOperation index  2 read GetFlagOperation write SetFlagOperation
      default foNone;
    property ZF  : TZYFlagOperation index  3 read GetFlagOperation write SetFlagOperation
      default foNone;
    property SF  : TZYFlagOperation index  4 read GetFlagOperation write SetFlagOperation
      default foNone;
    property TF  : TZYFlagOperation index  5 read GetFlagOperation write SetFlagOperation
      default foNone;
    property &IF : TZYFlagOperation index  6 read GetFlagOperation write SetFlagOperation
      default foNone;
    property DF  : TZYFlagOperation index  7 read GetFlagOperation write SetFlagOperation
      default foNone;
    property &OF : TZYFlagOperation index  8 read GetFlagOperation write SetFlagOperation
      default foNone;
    property IOPL: TZYFlagOperation index  9 read GetFlagOperation write SetFlagOperation
      default foNone;
    property NT  : TZYFlagOperation index 10 read GetFlagOperation write SetFlagOperation
      default foNone;
    property RF  : TZYFlagOperation index 11 read GetFlagOperation write SetFlagOperation
      default foNone;
    property VM  : TZYFlagOperation index 12 read GetFlagOperation write SetFlagOperation
      default foNone;
    property AC  : TZYFlagOperation index 13 read GetFlagOperation write SetFlagOperation
      default foNone;
    property VIF : TZYFlagOperation index 14 read GetFlagOperation write SetFlagOperation
      default foNone;
    property VIP : TZYFlagOperation index 15 read GetFlagOperation write SetFlagOperation
      default foNone;
    property ID  : TZYFlagOperation index 16 read GetFlagOperation write SetFlagOperation
      default foNone;
    property C0  : TZYFlagOperation index 17 read GetFlagOperation write SetFlagOperation
      default foNone;
    property C1  : TZYFlagOperation index 18 read GetFlagOperation write SetFlagOperation
      default foNone;
    property C2  : TZYFlagOperation index 19 read GetFlagOperation write SetFlagOperation
      default foNone;
    property C3  : TZYFlagOperation index 20 read GetFlagOperation write SetFlagOperation
      default foNone;
  end;

  TZYInstructionVEXInfo = class sealed(TZYJSONODefinitionComposite)
  strict private
    FStaticBroadcast: TZYStaticBroadcast;
  strict private
    procedure SetStaticBroadcast(Value: TZYStaticBroadcast); inline;
  protected
    procedure AssignTo(Dest: TPersistent); override;
  protected
    constructor Create(Definition: TZYInstructionDefinition);
  public
    procedure LoadFromJSON(const JSON: IJSONObjectReader); override;
    procedure SaveToJSON(const JSON: IJSONObjectWriter); override;
  public
    function Equals(Obj: TObject): Boolean; override;
  public
    destructor Destroy; override;
  published
    property StaticBroadcast: TZYStaticBroadcast read FStaticBroadcast
      write SetStaticBroadcast default sbcNone;
  end;

  TZYInstructionEVEXInfo = class sealed(TZYJSONODefinitionComposite)
  strict private
    FVectorLength: TZYVectorLength;
    FFunctionality: TZYEVEXFunctionality;
    FMaskMode: TZYMEVEXMaskMode;
    FMaskFlags: TZYEVEXMaskFlags;
    FTupleType: TZYEVEXTupleType;
    FElementSize: TZYEVEXElementSize;
    FStaticBroadcast: TZYStaticBroadcast;
  strict private
    procedure SetVectorLength(Value: TZYVectorLength); inline;
    procedure SetFunctionality(Value: TZYEVEXFunctionality); inline;
    procedure SetMaskMode(Value: TZYMEVEXMaskMode); inline;
    procedure SetMaskFlags(Value: TZYEVEXMaskFlags); inline;
    procedure SetTupleType(Value: TZYEVEXTupleType); inline;
    procedure SetElementSize(Value: TZYEVEXElementSize); inline;
    procedure SetStaticBroadcast(Value: TZYStaticBroadcast); inline;
  protected
    procedure AssignTo(Dest: TPersistent); override;
  protected
    constructor Create(Definition: TZYInstructionDefinition);
  public
    procedure LoadFromJSON(const JSON: IJSONObjectReader); override;
    procedure SaveToJSON(const JSON: IJSONObjectWriter); override;
  public
    function Equals(Obj: TObject): Boolean; override;
  public
    destructor Destroy; override;
  published
    property VectorLength: TZYVectorLength read FVectorLength write SetVectorLength
      default vlDefault;
    property Functionality: TZYEVEXFunctionality read FFunctionality write SetFunctionality
      default evInvalid;
    property MaskMode: TZYMEVEXMaskMode read FMaskMode write SetMaskMode default mmMaskInvalid;
    property MaskFlags: TZYEVEXMaskFlags read FMaskFlags write SetMaskFlags
      default [mfAcceptsZeroMask];
    property TupleType: TZYEVEXTupleType read FTupleType write SetTupleType default ttInvalid;
    property ElementSize: TZYEVEXElementSize read FElementSize write SetElementSize
      default esInvalid;
    property StaticBroadcast: TZYStaticBroadcast read FStaticBroadcast
      write SetStaticBroadcast default sbcNone;
  end;

  TZYInstructionMVEXInfo = class sealed(TZYJSONODefinitionComposite)
  strict private
    FFunctionality: TZYMVEXFunctionality;
    FMaskMode: TZYMEVEXMaskMode;
    FHasElementGranularity: Boolean;
    FStaticBroadcast: TZYStaticBroadcast;
  strict private
    function GetVectorLength: TZYVectorLength; inline;
  strict private
    procedure SetFunctionality(Value: TZYMVEXFunctionality); inline;
    procedure SetMaskMode(Value: TZYMEVEXMaskMode); inline;
    procedure SetHasElementGranularity(Value: Boolean); inline;
    procedure SetStaticBroadcast(Value: TZYStaticBroadcast); inline;
  protected
    procedure AssignTo(Dest: TPersistent); override;
  protected
    constructor Create(Definition: TZYInstructionDefinition);
  public
    procedure LoadFromJSON(const JSON: IJSONObjectReader); override;
    procedure SaveToJSON(const JSON: IJSONObjectWriter); override;
  public
    function Equals(Obj: TObject): Boolean; override;
  public
    destructor Destroy; override;
  published
    property VectorLength: TZYVectorLength read GetVectorLength default vlFixed512;
    property Functionality: TZYMVEXFunctionality read FFunctionality write SetFunctionality
      default mvIgnored;
    property MaskMode: TZYMEVEXMaskMode read FMaskMode write SetMaskMode default mmMaskInvalid;
    property HasElementGranularity: Boolean read FHasElementGranularity
      write SetHasElementGranularity default false;
    property StaticBroadcast: TZYStaticBroadcast read FStaticBroadcast
      write SetStaticBroadcast default sbcNone;
  end;

  TZYInstructionPartDisplacement = record
  public
    Width16: Byte;
    Width32: Byte;
    Width64: Byte;
  end;

  TZYInstructionPartImmediate = record
  public
    Width16: Byte;
    Width32: Byte;
    Width64: Byte;
    IsSigned: Boolean;
    IsRelative: Boolean;
  end;

  TZYInstructionPart = (
    ipOpcode,
    ipModrm,
    ipDisplacement,
    ipImmediate0,
    ipImmediate1,
    ipForceRegForm
  );
  TZYInstructionParts = set of TZYInstructionPart;

  TZYInstructionPartInfo = class sealed(TObject)
  strict private
    FParts: TZYInstructionParts;
    FDisplacement: TZYInstructionPartDisplacement;
    FImmediates: array[0..1] of TZYInstructionPartImmediate;
  strict private
    function GetImmediate(Index: Integer): TZYInstructionPartImmediate; inline;
  private
    procedure Update(Definition: TZYInstructionDefinition);
  public
    function Equals(Obj: TObject): Boolean; override;
  public
    constructor Create;
  public
    property Parts: TZYInstructionParts read FParts;
    property Displacement: TZYInstructionPartDisplacement read FDisplacement;
    property ImmediateA: TZYInstructionPartImmediate index 0 read GetImmediate;
    property ImmediateB: TZYInstructionPartImmediate index 1 read GetImmediate;
  end;

  TZYInstructionMnemonic  = String;
  TZYOpcode               = 0..255;
  TZYCPUPrivilegeLevel    = 0..  3;

  TZYInstructionDefinition = class sealed(TPersistent)
  strict private
    FEditor: TZYInstructionEditor;
    FParent: TZYInstructionTreeNode;
    FFilterIndex: array[TZYInstructionFilterClass] of Integer;
    FInserted: Boolean;
    FUpdateCount: Integer;
    FNeedsUpdatePattern: Boolean;
    FNeedsUpdateProperties: Boolean;
    FHasConflicts: Boolean;
    FMnemonic: TZYInstructionMnemonic;
    FEncoding: TZYInstructionEncoding;
    FOpcodeMap: TZYOpcodeMap;
    FFilters: TZYInstructionFilters;
    FOperands: TZYInstructionOperands;
    FOperandSizeMap: TZYOperandSizeMap;
    FPrivilegeLevel: TZYCPUPrivilegeLevel;
    FFlags: TZYDefinitionFlags;
    FMetaInfo: TZYInstructionMetaInfo;
    FPrefixFlags: TZYPrefixFlags;
    FAffectedFlags: TZYInstructionFlagsInfo;
    FVEXInfo: TZYInstructionVEXInfo;
    FEVEXInfo: TZYInstructionEVEXInfo;
    FMVEXInfo: TZYInstructionMVEXInfo;
    FExceptionClass: TZYExceptionClass;
    FInstructionParts: TZYInstructionPartInfo;
    FComment: String;
    FData: Pointer;
  strict private
    function GetFilterIndex(FilterClass: TZYInstructionFilterClass): Integer; inline;
    function GetOpcode: TZYOpcode; inline;
  strict private
    procedure SetFilterIndex(FilterClass: TZYInstructionFilterClass; Value: Integer); inline;
    procedure SetMnemonic(const Value: TZYInstructionMnemonic); inline;
    procedure SetEncoding(Value: TZYInstructionEncoding); inline;
    procedure SetOpcodeMap(Value: TZYOpcodeMap); inline;
    procedure SetOpcode(Value: TZYOpcode); inline;
    procedure SetOperandSizeMap(Value: TZYOperandSizeMap); inline;
    procedure SetPrivilegeLevel(Value: TZYCPUPrivilegeLevel); inline;
    procedure SetFlags(Value: TZYDefinitionFlags); inline;
    procedure SetPrefixFlags(Value: TZYPrefixFlags); inline;
    procedure SetExceptionClass(Value: TZYExceptionClass); inline;
    procedure SetComment(const Value: String); inline;
  strict private
    procedure CheckMnemonic(const Value: String); inline;
    procedure UpdateConflictState; inline;
    procedure UpdateSpecialFilters; inline;
  private
    procedure SetParent(const Value: TZYInstructionTreeNode); inline;
  protected
    procedure ChangedPattern; inline;
    procedure ChangedProperties; inline;
  protected
    procedure AssignTo(Dest: TPersistent); override;
  protected
    constructor Create(Editor: TZYInstructionEditor; const Mnemonic: String);
  public
    procedure BeginUpdate; inline;
    procedure UpdatePattern; inline;
    procedure UpdateProperties; inline;
    procedure EndUpdate; inline;
  public
    procedure Insert; inline;
    procedure Remove; inline;
  public
    procedure LoadFromJSON(const JSON: IJSONObjectReader);
    procedure SaveToJSON(const JSON: IJSONObjectWriter);
  public
    procedure GetConflictMessages(var ConflictMessages: TArray<String>); inline;
  public
    function CompareTo(Other: TZYInstructionDefinition): Integer;
    function HasEqualPattern(Definition: TZYInstructionDefinition): Boolean;
    function HasEqualProperties(Definition: TZYInstructionDefinition): Boolean;
    function Equals(Obj: TObject): Boolean; overload; override;
  public
    destructor Destroy; override;
  public
    property Editor: TZYInstructionEditor read FEditor;
    property Parent: TZYInstructionTreeNode read FParent;
    property Inserted: Boolean read FInserted;
    property FilterIndex[Filter: TZYInstructionFilterClass]: Integer read GetFilterIndex
      write SetFilterIndex;
    property InstructionParts: TZYInstructionPartInfo read FInstructionParts;
    property HasConflicts: Boolean read FHasConflicts;
    property Data: Pointer read FData write FData;
  published
    property Mnemonic: TZYInstructionMnemonic read FMnemonic write SetMnemonic;
    property Encoding: TZYInstructionEncoding read FEncoding write SetEncoding;
    property OpcodeMap: TZYOpcodeMap read FOpcodeMap write SetOpcodeMap;
    property Opcode: TZYOpcode read GetOpcode write SetOpcode;
    property Filters: TZYInstructionFilters read FFilters;
    property Operands: TZYInstructionOperands read FOperands;
    property OperandSizeMap: TZYOperandSizeMap read FOperandSizeMap write SetOperandSizeMap
      default osmDefault;
    property PrivilegeLevel: TZYCPUPrivilegeLevel read FPrivilegeLevel write SetPrivilegeLevel
      default 3;
    property Flags: TZYDefinitionFlags read FFlags write SetFlags default [];
    property Meta: TZYInstructionMetaInfo read FMetaInfo;
    property PrefixFlags: TZYPrefixFlags read FPrefixFlags write SetPrefixFlags default [];
    property ExceptionClass: TZYExceptionClass read FExceptionClass write SetExceptionClass
      default ecNone;
    property AffectedFlags: TZYInstructionFlagsInfo read FAffectedFlags;
    property VEX: TZYInstructionVEXInfo read FVEXInfo;
    property EVEX: TZYInstructionEVEXInfo read FEVEXInfo;
    property MVEX: TZYInstructionMVEXInfo read FMVEXInfo;

    property Comment: String read FComment write SetComment;
  end;

  EZYDefinitionJSONException = class(Exception)
  strict private
    FDefinitionNumber: Integer;
    FJSONString: String;
  public
    property DefinitionNumber: Integer read FDefinitionNumber write FDefinitionNumber;
    property JSONString: String read FJSONString write FJSONString;
  end;

  TZYEditorWorkStartEvent =
    procedure(Sender: TObject; MinWorkCount, MaxWorkCount: Integer) of Object;
  TZYEditorWorkEvent =
    procedure(Sender: TObject; WorkCount: Integer) of Object;
  TZYEditorWorkEndEvent = TNotifyEvent;
  TZYEditorNodeEvent =
    procedure(Sender: TObject; Node: TZYInstructionTreeNode) of Object;
  TZYEditorDefinitionEvent =
    procedure(Sender: TObject; Definition: TZYInstructionDefinition) of Object;

  TZYInstructionEditor = class sealed(TObject)
  private
    const CREATED   = 0;
    const INSERTED  = 1;
    const CHANGED   = 2;
    const REMOVED   = 3;
    const DESTROYED = 4;
  strict private
    FUpdateCount: Integer;
    FDefinitions: TList<TZYInstructionDefinition>;
    FRootNode: TZYInstructionTreeNode;
    FDelayDefinitionRegistration: Boolean;
    FPreventDefinitionRemoval: Boolean;
  strict private
    FOnWorkStart: TZYEditorWorkStartEvent;
    FOnWork: TZYEditorWorkEvent;
    FOnWorkEnd: TZYEditorWorkEndEvent;
    FOnBeginUpdate: TNotifyEvent;
    FOnEndUpdate: TNotifyEvent;
    FNodeEvent: array[0..4] of TZYEditorNodeEvent;
    FDefinitionEvent: array[0..4] of TZYEditorDefinitionEvent;
  private
    FFilterCount: array[TZYInstructionFilterClass] of Integer;
    FFilterCountTotal: Integer;
  strict private
    function GetDefinition(Index: Integer): TZYInstructionDefinition; inline;
    function GetDefinitionCount: Integer; inline;
    function GetNodeEvent(Index: Integer): TZYEditorNodeEvent; inline;
    function GetDefinitionEvent(Index: Integer): TZYEditorDefinitionEvent; inline;
    function GetFilterCount(FilterClass: TZYInstructionFilterClass): Integer; inline;
  strict private
    procedure SetNodeEvent(Index: Integer; const Value: TZYEditorNodeEvent); inline;
    procedure SetDefinitionEvent(Index: Integer; const Value: TZYEditorDefinitionEvent); inline;
  strict private
    function GetDefinitionTopLevelFilter(
      Definition: TZYInstructionDefinition): TZYInstructionTreeNode; inline;
  private
    procedure RegisterDefinition(Definition: TZYInstructionDefinition);
    procedure UnregisterDefinition(Definition: TZYInstructionDefinition); inline;
    procedure InsertDefinition(Definition: TZYInstructionDefinition);
    procedure RemoveDefinition(Definition: TZYInstructionDefinition);
    procedure DefinitionChanged(Definition: TZYInstructionDefinition); inline;
  private
    procedure RaiseNodeEvent(Id: Integer; Node: TZYInstructionTreeNode); inline;
    procedure RaiseDefinitionEvent(Id: Integer; Definition: TZYInstructionDefinition); inline;
  public
    procedure BeginUpdate; inline;
    procedure EndUpdate; inline;
  public
    procedure LoadFromJSON(const JSON: IJSONArrayReader; DoReset: Boolean = true);
    procedure SaveToJSON(const JSON: IJSONArrayWriter);
    procedure LoadFromFile(const Filename: String);
    procedure SaveToFile(const Filename: String);
    procedure Reset;
  public
    function CreateDefinition(const Mnemonic: String): TZYInstructionDefinition; inline;
  public
    constructor Create;
    destructor Destroy; override;
  public
    property RootNode: TZYInstructionTreeNode read FRootNode;
    property Definitions[Index: Integer]: TZYInstructionDefinition read GetDefinition;
    property DefinitionCount: Integer read GetDefinitionCount;
    property FilterCount[FilterClass: TZYInstructionFilterClass]: Integer read GetFilterCount;
    property FilterCountTotal: Integer read FFilterCountTotal;
  public
    property OnWorkStart: TZYEditorWorkStartEvent read FOnWorkStart write FOnWorkStart;
    property OnWork: TZYEditorWorkEvent read FOnWork write FOnWork;
    property OnWorkEnd: TZYEditorWorkEndEvent read FOnWorkEnd write FOnWorkEnd;
    property OnBeginUpdate: TNotifyEvent read FOnBeginUpdate write FOnBeginUpdate;
    property OnEndUpdate: TNotifyEvent read FOnEndUpdate write FOnEndUpdate;
    property OnNodeCreated: TZYEditorNodeEvent
      index CREATED   read GetNodeEvent write SetNodeEvent;
    property OnNodeInserted: TZYEditorNodeEvent
      index INSERTED  read GetNodeEvent write SetNodeEvent;
    property OnNodeChanged: TZYEditorNodeEvent
      index CHANGED   read GetNodeEvent write SetNodeEvent;
    property OnNodeRemoved: TZYEditorNodeEvent
      index REMOVED   read GetNodeEvent write SetNodeEvent;
    property OnNodeDestroyed: TZYEditorNodeEvent
      index DESTROYED read GetNodeEvent write SetNodeEvent;
    property OnDefinitionCreated: TZYEditorDefinitionEvent
      index CREATED   read GetDefinitionEvent write SetDefinitionEvent;
    property OnDefinitionInserted: TZYEditorDefinitionEvent
      index INSERTED  read GetDefinitionEvent write SetDefinitionEvent;
    property OnDefinitionChanged: TZYEditorDefinitionEvent
      index CHANGED   read GetDefinitionEvent write SetDefinitionEvent;
    property OnDefinitionRemoved: TZYEditorDefinitionEvent
      index REMOVED   read GetDefinitionEvent write SetDefinitionEvent;
    property OnDefinitionDestroyed: TZYEditorDefinitionEvent
      index DESTROYED read GetDefinitionEvent write SetDefinitionEvent;
  end;

implementation

uses
  System.Math, System.Variants, System.Generics.Defaults, System.NetEncoding, Utils.Comparator,
  Zydis.Validator;

{$REGION 'Class: TZYBasePersistent'}
procedure TZYBasePersistent.BeginUpdate;
begin
  Inc(FUpdateCount);
end;

constructor TZYBasePersistent.Create;
begin
  inherited Create;

end;

procedure TZYBasePersistent.EndUpdate;
begin
  if (FUpdateCount = 0) then
  begin
    raise Exception.Create('Invalid action');
  end;
  Dec(FUpdateCount);
  if (FUpdateCount = 0) and (FUpdateRequired) then
  begin
    FUpdateRequired := false;
    Changed;
  end;
end;

procedure TZYBasePersistent.Update;
begin
  if (FUpdateCount = 0) then
  begin
    Changed;
  end else
  begin
    FUpdateRequired := true;
  end;
end;
{$ENDREGION}

{$REGION 'Class: TZYLinkedPersistent'}
procedure TZYLinkedPersistent<T>.Changed;
begin
  FParent.Changed;
end;

constructor TZYLinkedPersistent<T>.Create(Parent: T);
begin
  inherited Create;
  FParent := Parent;
end;
{$ENDREGION}

{$REGION 'Class: TZYDefinitionComposite'}
procedure TZYDefinitionComposite.Changed;
begin
  FDefinition.UpdateProperties;
end;

constructor TZYDefinitionComposite.Create(Definition: TZYInstructionDefinition);
begin
  inherited Create;
  FDefinition := Definition;
end;
{$ENDREGION}

{$REGION 'Class: TZYInstructionTreeNode'}
procedure TZYInstructionTreeNode.Changed;
begin
  FEditor.RaiseNodeEvent(TZYInstructionEditor.CHANGED, Self);
end;

constructor TZYInstructionTreeNode.Create(Editor: TZYInstructionEditor;
  FilterClass: TZYInstructionFilterClass; IsRootNode, IsStaticNode: Boolean);
var
  Info: TZYInstructionFilter;
begin
  inherited Create;

  FEditor := Editor;
  FFilterClass := FilterClass;

  if (IsRootNode  ) then Include(FFlags, itnfIsRootNode);
  if (IsStaticNode) then Include(FFlags, itnfIsStaticNode);

  if (FFilterClass = ifcInvalid) then
  begin
    FChildCapacity := 0;
    Include(FFlags, itnfIsLeafNode);
  end else
  begin
    Inc(Editor.FFilterCount[FFilterClass]);
    Inc(Editor.FFilterCountTotal);
    Info := TZYInstructionFilterInfo.Info[FFilterClass];
    Assert(not Info.IsCodeGenOnly);
    FChildCapacity := Info.TotalCapacity;
    SetLength(FChilds, FChildCapacity);
  end;

  FEditor.RaiseNodeEvent(TZYInstructionEditor.CREATED, Self);
end;

procedure TZYInstructionTreeNode.CreateChildNodeAtIndex(Index: Integer;
  FilterClass: TZYInstructionFilterClass; IsRootNode, IsStaticNode: Boolean);
begin
  SetChildItem(Index, TZYInstructionTreeNode.Create(Editor, FilterClass, IsRootNode, IsStaticNode));
end;

procedure TZYInstructionTreeNode.DecDefinitionConflictCount;
begin
  Dec(FDefinitionConflicts);
  if (FDefinitionConflicts = 0) then
  begin
    SetConflicts(FConflicts - [itncDefinitionConflict]);
  end;
  if Assigned(FParent) then
  begin
    FParent.DecInheritedConflictCount;
  end;
end;

procedure TZYInstructionTreeNode.DecInheritedConflictCount;
begin
  Dec(FInheritedConflicts);
  if (FInheritedConflicts = 0) then
  begin
    SetConflicts(FConflicts - [itncInheritedConflict]);
  end;
  if Assigned(FParent) then
  begin
    FParent.DecInheritedConflictCount;
  end;
end;

destructor TZYInstructionTreeNode.Destroy;
begin
  Assert((FChildCount = 0) and (not Assigned(FDefinitions)) and (FParent = nil));
  if (FFilterClass <> ifcInvalid) then
  begin
    Dec(Editor.FFilterCount[FFilterClass]);
    Dec(Editor.FFilterCountTotal);
  end;
  FEditor.RaiseNodeEvent(TZYInstructionEditor.DESTROYED, Self);
  inherited;
end;

function TZYInstructionTreeNode.GetChild(Index: Integer): TZYInstructionTreeNode;
begin
  Assert((Index >= 0) and (Index < Length(FChilds)));
  Result := FChilds[Index];
end;

function TZYInstructionTreeNode.GetDefinition(Index: Integer): TZYInstructionDefinition;
begin
  Result := nil;
  if Assigned(FDefinitions) then
  begin
    Assert((Index >= 0) and (Index < FDefinitions.Count));
    Result := FDefinitions[Index];
  end;
end;

function TZYInstructionTreeNode.GetDefinitionCount: Integer;
begin
  Result := 0;
  if Assigned(FDefinitions) then
  begin
    Result := FDefinitions.Count;
  end;
end;

function TZYInstructionTreeNode.HasConflicts: Boolean;
begin
  Result := (FConflicts <> []);
end;

procedure TZYInstructionTreeNode.IncDefinitionConflictCount;
begin
  Inc(FDefinitionConflicts);
  if (FDefinitionConflicts = 1) then
  begin
    SetConflicts(FConflicts + [itncDefinitionConflict]);
  end;
  if Assigned(FParent) then
  begin
    FParent.IncInheritedConflictCount;
  end;
end;

procedure TZYInstructionTreeNode.IncInheritedConflictCount;
begin
  Inc(FInheritedConflicts);
  if (FInheritedConflicts = 1) then
  begin
    SetConflicts(FConflicts + [itncInheritedConflict]);
  end;
  if Assigned(FParent) then
  begin
    FParent.IncInheritedConflictCount;
  end;
end;

function TZYInstructionTreeNode.IndexOf(ChildNode: TZYInstructionTreeNode): Integer;
var
  I: Integer;
begin
  Result := -1;
  for I := Low(FChilds) to High(FChilds) do
  begin
    if (FChilds[I] = ChildNode) then
    begin
      Result := I;
      Exit;
    end;
  end;
end;

procedure TZYInstructionTreeNode.InsertDefinition(Definition: TZYInstructionDefinition);
begin
  if (not Assigned(FDefinitions)) then
  begin
    FDefinitions := TList<TZYInstructionDefinition>.Create;
  end;

  Assert(FDefinitions.IndexOf(Definition) < 0);

  FDefinitions.Add(Definition);
  Definition.SetParent(Self);
  if (FDefinitions.Count = 2) then
  begin
    SetConflicts(FConflicts + [itncDefinitionCount]);
    if (Assigned(FParent)) then
    begin
      FParent.IncInheritedConflictCount;
    end;
  end;

  // TODO: I disabled this call for performance reasons. We don't really need to know the number
  //       of instructions in realtime.
  // Update;
end;

procedure TZYInstructionTreeNode.RemoveDefinition(Definition: TZYInstructionDefinition);
begin
  Assert(FDefinitions.IndexOf(Definition) >= 0);

  if (FDefinitions.Count = 2) then
  begin
    SetConflicts(FConflicts - [itncDefinitionCount]);
    if (Assigned(FParent)) then
    begin
      FParent.DecInheritedConflictCount;
    end;
  end;
  Definition.SetParent(nil);
  FDefinitions.Remove(Definition);
  if (FDefinitions.Count = 0) then
  begin
    FreeAndNil(FDefinitions);
  end;

  // TODO: I disabled this call for performance reasons. We don't really need to know the number
  //       of instructions in realtime.
  // Update;
end;

procedure TZYInstructionTreeNode.SetChildItem(Index: Integer; const Value: TZYInstructionTreeNode);
var
  I, C, V: Integer;
  B: Boolean;
begin
  Assert((Index >= 0) and (Index < Length(FChilds)));

  if (FChilds[Index] <> Value) then
  begin
    if (Assigned(Value) and (not Assigned(FChilds[Index]))) then
    begin
      Inc(FChildCount);
    end else
    if (not Assigned(Value) and (Assigned(FChilds[Index]))) then
    begin
      Dec(FChildCount);
    end;
    if (Assigned(FChilds[Index])) then
    begin
      FChilds[Index].SetParent(nil);
    end;
    FChilds[Index] := Value;
    if (Assigned(Value)) then
    begin
      FChilds[Index].SetParent(Self);
    end;

    // Update placeholder-slot conflict
    if (TZYInstructionFilterInfo.Info[FFilterClass].IsOptional) then
    begin
      if (Assigned(FChilds[TZYInstructionFilterInfo.Info[FFilterClass].IndexPlaceholder])) and
        (FChildCount > 1) then
      begin
        if (not (itncPlaceholderConflict in FConflicts)) then
        begin
          SetConflicts(FConflicts + [itncPlaceholderConflict]);
          if (Assigned(FParent)) then
          begin
            FParent.IncInheritedConflictCount;
          end;
        end;
      end else
      begin
        if (itncPlaceholderConflict in FConflicts) then
        begin
          SetConflicts(FConflicts - [itncPlaceholderConflict]);
          if (Assigned(FParent)) then
          begin
            FParent.DecInheritedConflictCount;
          end;
        end;
      end;
    end;

    // Update negated-value conflict
    if (TZYInstructionFilterInfo.Info[FFilterClass].SupportsNegatedValues) then
    begin
      B := false;
      if (FChildCount > 1) then
      begin
        C := 0;
        V := 0;
        for I :=
          TZYInstructionFilterInfo.Info[FFilterClass].IndexNegatedValueLo to
          TZYInstructionFilterInfo.Info[FFilterClass].IndexNegatedValueHi do
        begin
          if (Assigned(FChilds[I])) then
          begin
            V := I;
            Inc(C);
          end;
        end;
        B := (C > 1);
        if (C = 1) then
        begin
          {$WARNINGS OFF}
          B := not ((FChildCount = 2) and
            Assigned(FChilds[V - TZYInstructionFilterInfo.Info[FFilterClass].NumberOfValues]));
          {$WARNINGS ON}
        end;
      end;
      if (B) then
      begin
        if (not (itncNegatedValueConflict in FConflicts)) then
        begin
          SetConflicts(FConflicts + [itncNegatedValueConflict]);
          if (Assigned(FParent)) then
          begin
            FParent.IncInheritedConflictCount;
          end;
        end;
      end else
      begin
        if (itncNegatedValueConflict in FConflicts) then
        begin
          SetConflicts(FConflicts - [itncNegatedValueConflict]);
          if (Assigned(FParent)) then
          begin
            FParent.DecInheritedConflictCount;
          end;
        end;
      end;
    end;

    // TODO: I disabled this call for performance reasons. We don't really need to know the number
    //       of child-nodes in realtime.
    // Update;
  end;
end;

procedure TZYInstructionTreeNode.SetConflicts(const Value: TZYInstructionTreeNodeConflicts);
begin
  if (FConflicts <> Value) then
  begin
    FConflicts := Value;
    Update;
  end;
end;

procedure TZYInstructionTreeNode.SetParent(const Value: TZYInstructionTreeNode);
begin
  // If this node is a static one, the parent has to be a static-node as well
  Assert(
    (not Assigned(FParent)) or
    (Assigned(FParent) and (itnfIsStaticNode in FFlags) and (itnfIsStaticNode in FParent.Flags)) or
    (Assigned(FParent) and (not (itnfIsStaticNode in FFlags)))
  );

  if (FParent <> Value) then
  begin
    if (Assigned(FParent)) then
    begin
      FEditor.RaiseNodeEvent(TZYInstructionEditor.REMOVED, Self);
      if (HasConflicts) then
      begin
        FParent.DecInheritedConflictCount;
      end;
    end;
    FParent := Value;
    if (Assigned(Value)) then
    begin
      FEditor.RaiseNodeEvent(TZYInstructionEditor.INSERTED, Self);
      if (HasConflicts) then
      begin
        Value.IncInheritedConflictCount;
      end;
    end;

    // TODO: I disabled this call for performance reasons. We don't really need to know the parent
    //       of nodes in realtime.
    // Update;
  end;
end;
{$ENDREGION}

{$REGION 'Class: TZYInstructionFilters'}
procedure TZYInstructionFilters.AssignTo(Dest: TPersistent);
var
  D: TZYInstructionFilters;
begin
  if (Dest is TZYInstructionFilters) then
  begin
    D := TZYInstructionFilters(Dest);
    D.BeginUpdate;
    try
      D.SetMode(Mode);
      D.SetModrmMod(ModrmMod);
      D.SetModrmReg(ModrmReg);
      D.SetModrmRm(ModrmRm);
      D.SetMandatoryPrefix(MandatoryPrefix);
      D.SetOperandSize(OperandSize);
      D.SetAddressSize(AddressSize);
      D.SetVectorLength(VectorLength);
      D.SetRexW(RexW);
      D.SetRexB(RexB);
      D.SetEvexB(EvexB);
      D.SetMvexE(MvexE);

      D.SetModeAMD(ModeAMD);
      D.SetModeKNC(ModeKNC);
      D.SetModeMPX(ModeMPX);
      D.SetModeCET(ModeCET);
      D.SetModeLZCNT(ModeLZCNT);
      D.SetModeTZCNT(ModeTZCNT);

      D.SetForceModrmReg(FForceModrmReg);
      D.SetForceModrmRm(FForceModrmRm);
    finally
      D.EndUpdate;
    end;
  end else inherited;
end;

procedure TZYInstructionFilters.Changed;
begin
  Definition.UpdatePattern;
end;

constructor TZYInstructionFilters.Create(Definition: TZYInstructionDefinition);
begin
  inherited Create(Definition);

end;

function TZYInstructionFilters.Equals(Obj: TObject): Boolean;
var
  O: TZYInstructionFilters;
begin
  Result := false;
  if (Obj is TZYInstructionFilters) then
  begin
    O := TZYInstructionFilters(Obj);
    Result :=
      (Mode = O.Mode) and
      (ModrmMod = O.ModrmMod) and
      (ModrmReg = O.ModrmReg) and
      (ModrmRm = O.ModrmRm) and
      (MandatoryPrefix = O.MandatoryPrefix) and
      (OperandSize = O.OperandSize) and
      (AddressSize = O.AddressSize) and
      (VectorLength = O.VectorLength) and
      (RexW = O.RexW) and
      (RexB = O.RexB) and
      (EvexB = O.EvexB) and
      (MvexE = O.MvexE) and

      (ModeAMD = O.ModeAMD) and
      (ModeKNC = O.ModeKNC) and
      (ModeMPX = O.ModeMPX) and
      (ModeCET = O.ModeCET) and
      (ModeLZCNT = O.ModeLZCNT) and
      (ModeTZCNT = O.ModeTZCNT) and

      (FForceModrmReg = O.FForceModrmReg) and
      (FForceModrmRm = O.FForceModrmRm);
  end;
end;

function TZYInstructionFilters.GetAddressSize: TZYFilterAddressSize;
begin
  Result := TZYFilterAddressSize(Definition.FilterIndex[ifcAddressSize]);
end;

function TZYInstructionFilters.GetEvexB: TZYFilterEvexB;
begin
  Result := TZYFilterEvexB(Definition.FilterIndex[ifcEvexB]);
end;

function TZYInstructionFilters.GetModeAMD: TZYFilterBoolean;
begin
  Result := TZYFilterBoolean(Definition.FilterIndex[ifcModeAMD]);
end;

function TZYInstructionFilters.GetModeCET: TZYFilterBoolean;
begin
  Result := TZYFilterBoolean(Definition.FilterIndex[ifcModeCET]);
end;

function TZYInstructionFilters.GetModeKNC: TZYFilterBoolean;
begin
  Result := TZYFilterBoolean(Definition.FilterIndex[ifcModeKNC]);
end;

function TZYInstructionFilters.GetModeLZCNT: TZYFilterBoolean;
begin
  Result := TZYFilterBoolean(Definition.FilterIndex[ifcModeLZCNT]);
end;

function TZYInstructionFilters.GetModeMPX: TZYFilterBoolean;
begin
  Result := TZYFilterBoolean(Definition.FilterIndex[ifcModeMPX]);
end;

function TZYInstructionFilters.GetModeTZCNT: TZYFilterBoolean;
begin
  Result := TZYFilterBoolean(Definition.FilterIndex[ifcModeTZCNT]);
end;

function TZYInstructionFilters.GetMandatoryPrefix: TZYFilterMandatoryPrefix;
begin
  Result := TZYFilterMandatoryPrefix(Definition.FilterIndex[ifcMandatoryPrefix]);
end;

function TZYInstructionFilters.GetMode: TZYFilterMode;
begin
  Result := TZYFilterMode(Definition.FilterIndex[ifcMode]);
end;

function TZYInstructionFilters.GetModrmMod: TZYFilterModrmMod;
begin
  Result := TZYFilterModrmMod(Definition.FilterIndex[ifcModrmMod]);
end;

function TZYInstructionFilters.GetModrmReg: TZYFilterModrmReg;
begin
  Result := TZYFilterModrmReg(Definition.FilterIndex[ifcModrmReg]);
end;

function TZYInstructionFilters.GetModrmRm: TZYFilterModrmRm;
begin
  Result := TZYFilterModrmRm(Definition.FilterIndex[ifcModrmRm]);
end;

function TZYInstructionFilters.GetMvexE: TZYFilterMvexE;
begin
  Result := TZYFilterMvexE(Definition.FilterIndex[ifcMvexE]);
end;

function TZYInstructionFilters.GetOperandSize: TZYFilterOperandSize;
begin
  Result := TZYFilterOperandSize(Definition.FilterIndex[ifcOperandSize]);
end;

function TZYInstructionFilters.GetRexB: TZYFilterRexB;
begin
  Result := TZYFilterRexB(Definition.FilterIndex[ifcRexB]);
end;

function TZYInstructionFilters.GetRexW: TZYFilterRexW;
begin
  Result := TZYFilterRexW(Definition.FilterIndex[ifcRexW]);
end;

function TZYInstructionFilters.GetVectorLength: TZYFilterVectorLength;
begin
  Result := TZYFilterVectorLength(Definition.FilterIndex[ifcVectorLength]);
end;

procedure TZYInstructionFilters.LoadFromJSON(const JSON: IJSONObjectReader);
begin
  BeginUpdate;
  try
    SetMode(JSON.Reader.ReadEnum('mode', modePlaceholder, TZYEnumFilterMode.JSONStrings));
    SetModrmMod(JSON.Reader.ReadEnum(
      'modrm_mod', mdPlaceholder, TZYEnumFilterModrmMod.JSONStrings));
    SetModrmReg(JSON.Reader.ReadEnum(
      'modrm_reg', rgPlaceholder, TZYEnumFilterModrmReg.JSONStrings));
    SetModrmRm(JSON.Reader.ReadEnum(
      'modrm_rm', rmPlaceholder, TZYEnumFilterModrmRm.JSONStrings));
    SetMandatoryPrefix(JSON.Reader.ReadEnum(
      'mandatory_prefix', mpPlaceholder, TZYEnumFilterMandatoryPrefix.JSONStrings));
    SetOperandSize(JSON.Reader.ReadEnum(
      'operand_size', osPlaceholder, TZYEnumFilterOperandSize.JSONStrings));
    SetAddressSize(JSON.Reader.ReadEnum(
      'address_size', asPlaceholder, TZYEnumFilterAddressSize.JSONStrings));
    SetVectorLength(JSON.Reader.ReadEnum(
      'vector_length', vlPlaceholder, TZYEnumFilterVectorLength.JSONStrings));
    SetRexW(JSON.Reader.ReadEnum('rex_w', rwPlaceholder, TZYEnumFilterRexW.JSONStrings));
    SetRexB(JSON.Reader.ReadEnum('rex_b', rbPlaceholder, TZYEnumFilterRexB.JSONStrings));
    SetEvexB(JSON.Reader.ReadEnum('evex_b', ebPlaceholder, TZYEnumFilterEvexB.JSONStrings));
    SetMvexE(JSON.Reader.ReadEnum('mvex_e', mePlaceholder, TZYEnumFilterMvexE.JSONStrings));

    SetModeAMD(JSON.Reader.ReadEnum(
      'feature_amd', fbPlaceholder, TZYEnumFilterBoolean.JSONStrings));
    SetModeKNC(JSON.Reader.ReadEnum(
      'feature_knc', fbPlaceholder, TZYEnumFilterBoolean.JSONStrings));
    SetModeMPX(JSON.Reader.ReadEnum(
      'feature_mpx', fbPlaceholder, TZYEnumFilterBoolean.JSONStrings));
    SetModeCET(JSON.Reader.ReadEnum(
      'feature_cet', fbPlaceholder, TZYEnumFilterBoolean.JSONStrings));
    SetModeLZCNT(JSON.Reader.ReadEnum(
      'feature_lzcnt', fbPlaceholder, TZYEnumFilterBoolean.JSONStrings));
    SetModeTZCNT(JSON.Reader.ReadEnum(
      'feature_tzcnt', fbPlaceholder, TZYEnumFilterBoolean.JSONStrings));

    SetForceModrmReg(JSON.ReadBoolean('force_modrm_reg', false));
    SetForceModrmRm(JSON.ReadBoolean('force_modrm_rm', false));
  finally
    EndUpdate;
  end;
end;

procedure TZYInstructionFilters.SaveToJSON(const JSON: IJSONObjectWriter);
begin
  if (Mode <> modePlaceholder) then
    JSON.Writer.WriteEnum('mode', Mode, TZYEnumFilterMode.JSONStrings);
  if (ModrmMod <> mdPlaceholder) then
    JSON.Writer.WriteEnum('modrm_mod', ModrmMod, TZYEnumFilterModrmMod.JSONStrings);
  if (ModrmReg <> rgPlaceholder) then
    JSON.Writer.WriteEnum('modrm_reg', ModrmReg, TZYEnumFilterModrmReg.JSONStrings);
  if (ModrmRm <> rmPlaceholder) then
    JSON.Writer.WriteEnum('modrm_rm', ModrmRm, TZYEnumFilterModrmRm.JSONStrings);
  if (MandatoryPrefix <> mpPlaceholder) then
    JSON.Writer.WriteEnum('mandatory_prefix',
      MandatoryPrefix, TZYEnumFilterMandatoryPrefix.JSONStrings);
  if (OperandSize <> osPlaceholder) then
    JSON.Writer.WriteEnum('operand_size', OperandSize, TZYEnumFilterOperandSize.JSONStrings);
  if (AddressSize <> asPlaceholder) then
    JSON.Writer.WriteEnum('address_size', AddressSize, TZYEnumFilterAddressSize.JSONStrings);
  if (VectorLength <> vlPlaceholder) then
    JSON.Writer.WriteEnum('vector_length', VectorLength, TZYEnumFilterVectorLength.JSONStrings);
  if (RexW <> rwPlaceholder) then
    JSON.Writer.WriteEnum('rex_w', RexW, TZYEnumFilterRexW.JSONStrings);
  if (RexB <> rbPlaceholder) then
    JSON.Writer.WriteEnum('rex_b', RexB, TZYEnumFilterRexB.JSONStrings);
  if (EvexB <> ebPlaceholder) then
    JSON.Writer.WriteEnum('evex_b', EvexB, TZYEnumFilterEvexB.JSONStrings);
  if (MvexE <> mePlaceholder) then
    JSON.Writer.WriteEnum('mvex_e', MvexE, TZYEnumFilterMvexE.JSONStrings);

  if (ModeAMD <> fbPlaceholder) then
    JSON.Writer.WriteEnum('feature_amd', ModeAMD, TZYEnumFilterBoolean.JSONStrings);
  if (ModeKNC <> fbPlaceholder) then
    JSON.Writer.WriteEnum('feature_knc', ModeKNC, TZYEnumFilterBoolean.JSONStrings);
  if (ModeMPX <> fbPlaceholder) then
    JSON.Writer.WriteEnum('feature_mpx', ModeMPX, TZYEnumFilterBoolean.JSONStrings);
  if (ModeCET <> fbPlaceholder) then
    JSON.Writer.WriteEnum('feature_cet', ModeCET, TZYEnumFilterBoolean.JSONStrings);
  if (ModeLZCNT <> fbPlaceholder) then
    JSON.Writer.WriteEnum('feature_lzcnt', ModeLZCNT, TZYEnumFilterBoolean.JSONStrings);
  if (ModeTZCNT <> fbPlaceholder) then
    JSON.Writer.WriteEnum('feature_tzcnt', ModeTZCNT, TZYEnumFilterBoolean.JSONStrings);

  if (FForceModrmReg <> false) then
    JSON.WriteBoolean('force_modrm_reg', FForceModrmReg);
  if (FForceModrmRm <> false) then
    JSON.WriteBoolean('force_modrm_rm', FForceModrmRm);
end;

procedure TZYInstructionFilters.SetAddressSize(Value: TZYFilterAddressSize);
begin
  Definition.FilterIndex[ifcAddressSize] := Ord(Value);
end;

procedure TZYInstructionFilters.SetEvexB(Value: TZYFilterEvexB);
begin
  if (EvexB <> Value) then
  begin
    if (not (Definition.Encoding in [iencEVEX])) then
    begin
      raise EZYDefinitionPropertyException.CreateFmt(
        'The %s instruction-encoding does not support the EVEX_B filter.', [
        TZYEnumInstructionEncoding.ZydisStrings[Definition.Encoding]]);
    end;
    Definition.FilterIndex[ifcEvexB] := Ord(Value);
  end;
end;

procedure TZYInstructionFilters.SetModeAMD(Value: TZYFilterBoolean);
begin
  Definition.FilterIndex[ifcModeAMD] := Ord(Value);
end;

procedure TZYInstructionFilters.SetModeCET(Value: TZYFilterBoolean);
begin
  Definition.FilterIndex[ifcModeCET] := Ord(Value);
end;

procedure TZYInstructionFilters.SetModeKNC(Value: TZYFilterBoolean);
begin
  Definition.FilterIndex[ifcModeKNC] := Ord(Value);
end;

procedure TZYInstructionFilters.SetModeLZCNT(Value: TZYFilterBoolean);
begin
  Definition.FilterIndex[ifcModeLZCNT] := Ord(Value);
end;

procedure TZYInstructionFilters.SetModeMPX(Value: TZYFilterBoolean);
begin
  Definition.FilterIndex[ifcModeMPX] := Ord(Value);
end;

procedure TZYInstructionFilters.SetModeTZCNT(Value: TZYFilterBoolean);
begin
  Definition.FilterIndex[ifcModeTZCNT] := Ord(Value);
end;

procedure TZYInstructionFilters.SetForceModrmReg(Value: Boolean);
begin
  if (FForceModrmReg <> Value) then
  begin
    FForceModrmReg := Value;
    Definition.UpdateProperties;
  end;
end;

procedure TZYInstructionFilters.SetForceModrmRm(Value: Boolean);
begin
  if (FForceModrmRm <> Value) then
  begin
    FForceModrmRm := Value;
    Definition.UpdateProperties;
  end;
end;

procedure TZYInstructionFilters.SetMandatoryPrefix(Value: TZYFilterMandatoryPrefix);
begin
  if (MandatoryPrefix <> Value) then
  begin
    if (Definition.Encoding in [iencVEX, iencEVEX, iencMVEX]) and (Value = mpPlaceholder) then
    begin
      raise EZYDefinitionPropertyException.CreateFmt(
        'The %s instruction-encoding requires a MANDATORY_PREFIX filter.', [
        TZYEnumInstructionEncoding.ZydisStrings[Definition.Encoding]]);
    end;
    if (Definition.Encoding in [iencXOP]) and (Value <> mpNone) then
    begin
      raise EZYDefinitionPropertyException.Create(
        'The XOP instruction-encoding requires a MANDATORY_PREFIX value of NONE.');
    end;
    Definition.FilterIndex[ifcMandatoryPrefix] := Ord(Value);
  end;
end;

procedure TZYInstructionFilters.SetMode(Value: TZYFilterMode);
begin
  Definition.FilterIndex[ifcMode] := Ord(Value);
end;

procedure TZYInstructionFilters.SetModrmMod(Value: TZYFilterModrmMod);
begin
  Definition.FilterIndex[ifcModrmMod] := Ord(Value);
end;

procedure TZYInstructionFilters.SetModrmReg(Value: TZYFilterModrmReg);
begin
  Definition.FilterIndex[ifcModrmReg] := Ord(Value);
end;

procedure TZYInstructionFilters.SetModrmRm(Value: TZYFilterModrmRm);
begin
  Definition.FilterIndex[ifcModrmRm] := Ord(Value);
end;

procedure TZYInstructionFilters.SetMvexE(Value: TZYFilterMvexE);
begin
  if (MvexE <> Value) then
  begin
    if (not (Definition.Encoding in [iencMVEX])) then
    begin
      raise EZYDefinitionPropertyException.CreateFmt(
        'The %s instruction-encoding does not support the MVEX_E filter.', [
        TZYEnumInstructionEncoding.ZydisStrings[Definition.Encoding]]);
    end;
    Definition.FilterIndex[ifcMvexE] := Ord(Value);
  end;
end;

procedure TZYInstructionFilters.SetOperandSize(Value: TZYFilterOperandSize);
begin
  Definition.FilterIndex[ifcOperandSize] := Ord(Value);
end;

procedure TZYInstructionFilters.SetRexB(Value: TZYFilterRexB);
begin
  Definition.FilterIndex[ifcRexB] := Ord(Value);
end;

procedure TZYInstructionFilters.SetRexW(Value: TZYFilterRexW);
begin
  Definition.FilterIndex[ifcRexW] := Ord(Value);
end;

procedure TZYInstructionFilters.SetVectorLength(Value: TZYFilterVectorLength);
begin
  Definition.FilterIndex[ifcVectorLength] := Ord(Value);
end;
{$ENDREGION}

{$REGION 'Class: TZYInstructionOperands'}
procedure TZYInstructionOperands.AssignTo(Dest: TPersistent);
var
  D: TZYInstructionOperands;
  I: Integer;
begin
  if (Dest is TZYInstructionOperands) then
  begin
    D := TZYInstructionOperands(Dest);
    D.BeginUpdate;
    try
      for I := Low(FOperands) to High(FOperands) do
      begin
        D.FOperands[I].Assign(FOperands[I]);
      end;
    finally
      D.EndUpdate;
    end;
  end else inherited;
end;

procedure TZYInstructionOperands.Changed;
begin
  UpdateNumberOfUsedOperands;
  inherited;
end;

constructor TZYInstructionOperands.Create(Definition: TZYInstructionDefinition);
var
  I: Integer;
begin
  inherited Create(Definition);
  for I := Low(FOperands) to High(FOperands) do
  begin
    FOperands[I] := TZYInstructionOperand.Create(Self, I);
  end;
end;

destructor TZYInstructionOperands.Destroy;
var
  I: Integer;
begin
  for I := Low(FOperands) to High(FOperands) do
  begin
    FOperands[I].Free;
  end;
  inherited;
end;

function TZYInstructionOperands.Equals(Obj: TObject): Boolean;
var
  O: TZYInstructionOperands;
  I: Integer;
begin
  Result := true;
  if (Obj is TZYInstructionOperands) then
  begin
    O := TZYInstructionOperands(Obj);
    for I := Low(FOperands) to High(FOperands) do
    begin
      Result := Result and FOperands[I].Equals(O.FOperands[I]);
      if (not Result) then
      begin
        Break;
      end;
    end;
  end;
end;

function TZYInstructionOperands.GetOperand(Index: Integer): TZYInstructionOperand;
begin
  Assert((Index >= 0) and (Index < MAX_OPERAND_COUNT));
  Result := FOperands[Index];
end;

class function TZYInstructionOperands.GetOperandCount: Integer;
begin
  Result := MAX_OPERAND_COUNT;
end;

procedure TZYInstructionOperands.LoadFromJSON(const JSON: IJSONArrayReader);
var
  I: Integer;
  J: IJSONObjectReader;
begin
  BeginUpdate;
  try
    // Quick&Dirty Reset
    for I := Low(FOperands) to High(FOperands) do
    begin
      FOperands[I].Free;
      FOperands[I] := TZYInstructionOperand.Create(Self, I);
    end;
    for I := 0 to System.Math.Min(JSON.InnerObject.Count, Length(FOperands)) - 1 do
    begin
      if (JSON.ValueType(I) <> jsonNull) then
      begin
        J := JSON.ReadObject(I);
        FOperands[I].LoadFromJSON(J);
      end;
    end;
  finally
    EndUpdate;
  end;
end;

procedure TZYInstructionOperands.SaveToJSON(const JSON: IJSONArrayWriter);
var
  I, J: Integer;
begin
  J := -1;
  for I := Low(FOperands) to High(FOperands) do
  begin
    if (FOperands[I].OperandType <> optUnused) then J := I;
  end;
  for I := Low(FOperands) to J do
  begin
    if (FOperands[I].OperandType = optUnused) then
    begin
      JSON.AddNull;
    end else
    begin
      FOperands[I].SaveToJSON(JSON.AddObject);
    end;
  end;
end;

procedure TZYInstructionOperands.UpdateNumberOfUsedOperands;
var
  I: Integer;
begin
  FNumberOfUsedOperands := 0;
  for I := Low(FOperands) to High(FOperands) do
  begin
    if (FOperands[I].OperandType = optUnused) then Break;
    Inc(FNumberOfUsedOperands);
  end;
end;
{$ENDREGION}

{$REGION 'Class: TZYInstructionOperand'}
procedure TZYInstructionOperand.AssignTo(Dest: TPersistent);
var
  D: TZYInstructionOperand;
  I: Integer;
begin
  if (Dest is TZYInstructionOperand) then
  begin
    D := TZYInstructionOperand(Dest);
    D.BeginUpdate;
    try
      D.SetAction(FAction);
      D.SetOperandType(FOperandType);
      D.SetEncoding(FEncoding);
      D.SetRegister(FRegister);
      D.SetMemorySegment(FMemorySegment);
      D.SetMemoryBase(FMemoryBase);
      D.SetElementType(FElementType);
      D.SetScaleFactor(FScaleFactor);
      for I := Low(FWidth) to High(FWidth) do
      begin
        D.SetWidth(I, FWidth[I]);
      end;
      D.SetVisible(FVisible);
    finally
      D.EndUpdate;
    end;
  end else inherited;
end;

procedure TZYInstructionOperand.Changed;
begin
  // Operands can be created without a valid parent for use as automatically managed pseudo
  // operands (like R/E/FLAGS).
  if Assigned(Parent) then
  begin
    inherited;
  end;
end;

constructor TZYInstructionOperand.Create(Parent: TZYInstructionOperands; Index: Integer);
begin
  inherited Create(Parent);
  FIndex := Index;
  FVisible := true;
end;

destructor TZYInstructionOperand.Destroy;
begin

  inherited;
end;

function TZYInstructionOperand.Equals(Obj: TObject): Boolean;
var
  O: TZYInstructionOperand;
begin
  Result := false;
  if (Obj is TZYInstructionOperand) then
  begin
    O := TZYInstructionOperand(Obj);
    Result :=
      (O.FAction = FAction) and
      (O.FOperandType = FOperandType) and
      (O.FEncoding = FEncoding) and
      (O.FRegister = FRegister) and
      (O.FMemorySegment = FMemorySegment) and
      (O.FMemoryBase = FMemoryBase) and
      (O.FElementType = FElementType) and
      (O.FScaleFactor = FScaleFactor) and
      (O.FWidth[1] = FWidth[1]) and
      (O.FWidth[2] = FWidth[2]) and
      (O.FWidth[3] = FWidth[3]) and
      (O.FVisible = FVisible);
  end;
end;

function TZYInstructionOperand.GetVisibility: TZYOperandVisibility;
begin
  Result := opvExplicit;
  case FOperandType of
    optUnused:
      begin
        Result := opvUnused;
      end;
    optImplicitReg,
    optImplicitMem:
      begin
        if (not FVisible) then
        begin
          Result := opvHidden;
        end else
        begin
          Result := opvImplicit;
        end;
      end;
    optImplicitImm1:
      Result := opvImplicit;
  end;
end;

function TZYInstructionOperand.GetWidth(Index: Integer): TZYSemanticOperandWidth;
begin
  Result := FWidth[Index];
end;

procedure TZYInstructionOperand.LoadFromJSON(const JSON: IJSONObjectReader);
begin
  BeginUpdate;
  try
    SetAction(JSON.Reader.ReadEnum<TZYOperandAction>(
      'action', opaRead, TZYEnumOperandAction.JSONStrings));
    SetOperandType(JSON.Reader.ReadEnum<TZYOperandType>(
      'operand_type', optUnused, TZYEnumOperandType.JSONStrings));
    SetEncoding(JSON.Reader.ReadEnum<TZYOperandEncoding>(
      'encoding', opeNone, TZYEnumOperandEncoding.JSONStrings));
    SetRegister(JSON.Reader.ReadEnum<TZYRegister>(
      'register', regNone, TZYEnumRegister.JSONStrings));
    SetMemorySegment(JSON.Reader.ReadEnum<TZYSegmentRegister>(
      'mem_segment', sregNone, TZYSegmentRegister.JSONStrings));
    SetMemoryBase(JSON.Reader.ReadEnum<TZYBaseRegister>(
      'mem_base', bregNone, TZYBaseRegister.JSONStrings));
    SetElementType(JSON.Reader.ReadEnum<TZYElementType>(
      'element_type', emtInvalid, TZYEnumElementType.JSONStrings));
    SetScaleFactor(JSON.Reader.ReadEnum<TZYScaleFactor>(
      'scale_factor', sfStatic, TZYEnumScaleFactor.JSONStrings));
    SetWidth(1, JSON.ReadInteger('width16', 0));
    SetWidth(2, JSON.ReadInteger('width32', 0));
    SetWidth(3, JSON.ReadInteger('width64', 0));
    SetVisible(JSON.ReadBoolean('visible', true));
  finally
    EndUpdate;
  end;
end;

procedure TZYInstructionOperand.SaveToJSON(const JSON: IJSONObjectWriter);
begin
  if (FAction <> opaRead) then JSON.Writer.WriteEnum(
    'action', FAction, TZYEnumOperandAction.JSONStrings);
  if (FOperandType <> optUnused) then JSON.Writer.WriteEnum(
    'operand_type', FOperandType, TZYEnumOperandType.JSONStrings);
  if (FEncoding <> opeNone) then JSON.Writer.WriteEnum(
    'encoding', FEncoding, TZYEnumOperandEncoding.JSONStrings);
  if (FRegister <> regNone) then JSON.Writer.WriteEnum(
    'register', FRegister, TZYEnumRegister.JSONStrings);
  if (FMemorySegment <> sregNone) then JSON.Writer.WriteEnum(
    'mem_segment', FMemorySegment, TZYSegmentRegister.JSONStrings);
  if (FMemoryBase <> bregNone) then JSON.Writer.WriteEnum(
    'mem_base', FMemoryBase, TZYBaseRegister.JSONStrings);
  if (FElementType <> emtInvalid) then JSON.Writer.WriteEnum(
    'element_type', FElementType, TZYEnumElementType.JSONStrings);
  if (FScaleFactor <> sfStatic) then JSON.Writer.WriteEnum(
    'scale_factor', FScaleFactor, TZYEnumScaleFactor.JSONStrings);
  if (FWidth[1] <> 0) then JSON.WriteInteger('width16', FWidth[1]);
  if (FWidth[2] <> 0) then JSON.WriteInteger('width32', FWidth[2]);
  if (FWidth[3] <> 0) then JSON.WriteInteger('width64', FWidth[3]);
  if (not FVisible) then JSON.WriteBoolean('visible', FVisible);
end;

procedure TZYInstructionOperand.SetAction(const Value: TZYOperandAction);
begin
  if (FAction <> Value) then
  begin
    FAction := Value;
    Update;
  end;
end;

procedure TZYInstructionOperand.SetElementType(const Value: TZYElementType);
begin
  if (FElementType <> Value) then
  begin
    FElementType := Value;
    Update;
  end;
end;

procedure TZYInstructionOperand.SetEncoding(const Value: TZYOperandEncoding);
begin
  if (FEncoding <> Value) then
  begin
    FEncoding := Value;
    Update;
  end;
end;

procedure TZYInstructionOperand.SetMemoryBase(const Value: TZYBaseRegister);
begin
  if (FMemoryBase <> Value) then
  begin
    FMemoryBase := Value;
    Update;
  end;
end;

procedure TZYInstructionOperand.SetMemorySegment(const Value: TZYSegmentRegister);
begin
  if (FMemorySegment <> Value) then
  begin
    FMemorySegment := Value;
    Update;
  end;
end;

procedure TZYInstructionOperand.SetOperandType(const Value: TZYOperandType);
begin
  if (FOperandType <> Value) then
  begin
    FOperandType := Value;
    if (not (FOperandType in [optImplicitReg, optImplicitMem])) then
    begin
      FRegister := regNone;
      FMemorySegment := sregNone;
      FMemoryBase := bregNone;
      FVisible := true;
    end;
    // TODO: Set default encoding for the given operand-type
    case FOperandType of
      optImplicitReg:
        begin
          FEncoding := opeNone;
          FWidth[1] := 0;
          FWidth[2] := 0;
          FWidth[3] := 0;
        end;
      optImplicitMem:
        begin
          FEncoding := opeNone;
        end;
      // TODO:
    end;
    Update;
  end;
end;

procedure TZYInstructionOperand.SetRegister(const Value: TZYRegister);
begin
  if (FRegister <> Value) then
  begin
    FRegister := Value;
    Update;
  end;
end;

procedure TZYInstructionOperand.SetScaleFactor(const Value: TZYScaleFactor);
begin
  if (FScaleFactor <> Value) then
  begin
    FScaleFactor := Value;
    Update;
  end;
end;

procedure TZYInstructionOperand.SetVisible(const Value: Boolean);
begin
  if (FVisible <> Value) then
  begin
    FVisible := Value;
    Update;
  end;
end;

procedure TZYInstructionOperand.SetWidth(Index: Integer; const Value: TZYSemanticOperandWidth);
begin
  if (FWidth[Index] <> Value) then
  begin
    FWidth[Index] := Value;
    Update;
  end;
end;
{$ENDREGION}

{$REGION 'Class: TZYInstructionDisassembly'}
{
procedure TZYInstructionDisassembly.AssignTo(Dest: TPersistent);
var
  D: TZYInstructionDisassembly;
begin
  if (Dest is TZYInstructionDisassembly) then
  begin
    D := TZYInstructionDisassembly(Dest);
    D.BeginUpdate;
    try
      D.SetIntel(FIntel);
      D.SetATT(FATT);
    finally
      D.EndUpdate;
    end;
  end else inherited;
end;

constructor TZYInstructionDisassembly.Create(Definition: TZYInstructionDefinition);
begin
  inherited Create(Definition);
  FInheritedValues := [idvIntel, idvATT];
end;

destructor TZYInstructionDisassembly.Destroy;
begin

  inherited;
end;

function TZYInstructionDisassembly.Equals(Obj: TObject): Boolean;
var
  O: TZYInstructionDisassembly;
begin
  Result := false;
  if (Obj is TZYInstructionDisassembly) then
  begin
    O := TZYInstructionDisassembly(Obj);
    Result :=
      (O.GetIntel = GetIntel) and
      (O.GetATT = GetATT);
  end;
end;

function TZYInstructionDisassembly.GetATT: String;
begin
  if (idvATT in FInheritedValues) then
  begin
    Result := Definition.Mnemonic;
  end else
  begin
    Result := FATT;
  end;
end;

function TZYInstructionDisassembly.GetIntel: String;
begin
  if (idvIntel in FInheritedValues) then
  begin
    Result := Definition.Mnemonic;
  end else
  begin
    Result := FIntel;
  end;
end;

procedure TZYInstructionDisassembly.LoadFromJSON(const JSON: IJSONObjectReader);
begin

end;

procedure TZYInstructionDisassembly.SaveToJSON(const JSON: IJSONObjectWriter);
begin

end;

procedure TZYInstructionDisassembly.SetATT(const Value: String);
begin
  if (FATT <> Value) then
  begin
    FATT := Value;
    if (Value = '') or (GetATT = Value) then
    begin
      Include(FInheritedValues, idvATT);
    end else
    begin
      Exclude(FInheritedValues, idvATT);
    end;
    Update;
  end;
end;

procedure TZYInstructionDisassembly.SetInheritedValues(Value: TZYInheritedDisassemblyValues);
begin
  if (FIntel = '') then Include(Value, idvIntel);
  if (FATT   = '') then Include(Value, idvATT  );
  if (FInheritedValues <> Value) then
  begin
    FInheritedValues := Value;
    if (idvIntel in Value) then FIntel := '';
    if (idvATT   in Value) then FATT   := '';
    Update;
  end;
end;

procedure TZYInstructionDisassembly.SetIntel(const Value: String);
begin
  if (FIntel <> Value) then
  begin
    FIntel := Value;
    if (Value = '') or (GetIntel = Value) then
    begin
      Include(FInheritedValues, idvIntel);
    end else
    begin
      Exclude(FInheritedValues, idvIntel);
    end;
    Update;
  end;
end;
}
{$ENDREGION}

{$REGION 'Class: TZYInstructionMetaInfo'}
procedure TZYInstructionMetaInfo.AssignTo(Dest: TPersistent);
var
  D: TZYInstructionMetaInfo;
begin
  if (Dest is TZYInstructionMetaInfo) then
  begin
    D := TZYInstructionMetaInfo(Dest);
    D.BeginUpdate;
    try
      D.SetCategory(FCategory);
      D.SetExtension(FExtension);
      D.SetISASet(FISASet);
    finally
      D.EndUpdate;
    end;
  end else inherited;
end;

constructor TZYInstructionMetaInfo.Create(Definition: TZYInstructionDefinition);
begin
  inherited Create(Definition);

end;

destructor TZYInstructionMetaInfo.Destroy;
begin

  inherited;
end;

function TZYInstructionMetaInfo.Equals(Obj: TObject): Boolean;
var
  O: TZYInstructionMetaInfo;
begin
  Result := false;
  if (Obj is TZYInstructionMetaInfo) then
  begin
    O := TZYInstructionMetaInfo(Obj);
    Result :=
      (O.FCategory = FCategory) and
      (O.FExtension = FExtension) and
      (O.FISASet = FISASet);
  end;
end;

procedure TZYInstructionMetaInfo.LoadFromJSON(const JSON: IJSONObjectReader);
begin
  BeginUpdate;
  try
    SetCategory(JSON.ReadString(
      'category', ''));
    SetExtension(JSON.ReadString(
      'extension', ''));
    SetISASet(JSON.ReadString(
      'isa_set', ''));
  finally
    EndUpdate;
  end;
end;

procedure TZYInstructionMetaInfo.SaveToJSON(const JSON: IJSONObjectWriter);
begin
  if (FCategory <> '') then JSON.WriteString(
    'category', FCategory);
  if (FExtension <> '') then JSON.WriteString(
    'extension', FExtension);
  if (FISASet <> '') then JSON.WriteString(
    'isa_set', FISASet);
end;

procedure TZYInstructionMetaInfo.SetCategory(const Value: String);
begin
  if (FCategory <> Value) then
  begin
    FCategory := Value;
    Update;
  end;
end;

procedure TZYInstructionMetaInfo.SetExtension(const Value: String);
begin
  if (FExtension <> Value) then
  begin
    FExtension := Value;
    Update;
  end;
end;

procedure TZYInstructionMetaInfo.SetISASet(const Value: String);
begin
  if (FISASet <> Value) then
  begin
    FISASet := Value;
    Update;
  end;
end;
{$ENDREGION}

{$REGION 'Class: TZYInstructionFlagsInfo'}
procedure TZYInstructionFlagsInfo.AssignTo(Dest: TPersistent);
var
  D: TZYInstructionFlagsInfo;
  I: Integer;
begin
  if (Dest is TZYInstructionFlagsInfo) then
  begin
    D := TZYInstructionFlagsInfo(Dest);
    D.BeginUpdate;
    try
      D.SetAccess(FAccess);
      for I := Low(FFlags) to High(FFlags) do
      begin
        D.SetFlagOperation(I, FFlags[I]);
      end;
    finally
      D.EndUpdate;
    end;
  end else inherited;
end;

procedure TZYInstructionFlagsInfo.Changed;
begin
  UpdateManagedOperand;
  inherited;
end;

constructor TZYInstructionFlagsInfo.Create(Definition: TZYInstructionDefinition);
begin
  inherited Create(Definition);
  FManagedOperand := TZYInstructionOperand.Create(nil, 0);
  FManagedOperand.Register := regSSZFlags;
end;

destructor TZYInstructionFlagsInfo.Destroy;
begin
  FManagedOperand.Free;
  inherited;
end;

function TZYInstructionFlagsInfo.Equals(Obj: TObject): Boolean;
var
  O: TZYInstructionFlagsInfo;
  I: Integer;
begin
  Result := false;
  if (Obj is TZYInstructionFlagsInfo) then
  begin
    O := TZYInstructionFlagsInfo(Obj);
    for I := Low(FFlags) to High(FFlags) do
    begin
      if (O.FFlags[I] <> FFlags[I]) then
      begin
        Exit(false);
      end;
    end;
    Result := (O.FManagedOperand.Equals(FManagedOperand));
  end;
end;

function TZYInstructionFlagsInfo.GetFlagCount: Integer;
begin
  Result := Length(FFlags);
end;

function TZYInstructionFlagsInfo.GetFlagOperation(Index: Integer): TZYFlagOperation;
begin
  Assert((Index >= Low(FFlags)) and (Index <= High(FFlags)));
  Result := FFlags[Index];
end;

procedure TZYInstructionFlagsInfo.LoadFromJSON(const JSON: IJSONObjectReader);
const
  JSONIndizes: array[0..20] of String = (
    'cf', 'pf', 'af', 'zf', 'sf', 'tf', 'if', 'df', 'of', 'iopl', 'nt', 'rf', 'vm', 'ac', 'vif',
    'vip', 'id', 'c0', 'c1', 'c2', 'c3'
  );
var
  I: Integer;
begin
  BeginUpdate;
  try
    SetAccess(JSON.Reader.ReadEnum<TZYFlagsAccess>(
      'access', faReadOnly, TZYFlagsAccess.JSONStrings));
    Assert(Length(JSONIndizes) = Length(FFlags));
    for I := Low(FFlags) to High(FFlags) do
    begin
      SetFlagOperation(I, JSON.Reader.ReadEnum<TZYFlagOperation>(
        JSONIndizes[I], foNone, TZYFlagOperation.JSONStrings));
    end;
  finally
    EndUpdate;
  end;
end;

procedure TZYInstructionFlagsInfo.SaveToJSON(const JSON: IJSONObjectWriter);
const
  JSONIndizes: array[0..20] of String = (
    'cf', 'pf', 'af', 'zf', 'sf', 'tf', 'if', 'df', 'of', 'iopl', 'nt', 'rf', 'vm', 'ac', 'vif',
    'vip', 'id', 'c0', 'c1', 'c2', 'c3'
  );
var
  I: Integer;
begin
  if (FAccess <> faReadOnly) then JSON.Writer.WriteEnum<TZYFlagsAccess>(
    'access', FAccess, TZYFlagsAccess.JSONStrings);
  Assert(Length(JSONIndizes) = Length(FFlags));
  for I := Low(FFlags) to High(FFlags) do
  begin
    if (FFlags[I] = foNone) then Continue;
    JSON.Writer.WriteEnum<TZYFlagOperation>(
      JSONIndizes[I], FFlags[I], TZYFlagOperation.JSONStrings);
  end;
end;

procedure TZYInstructionFlagsInfo.SetAccess(Value: TZYFlagsAccess);
begin
  if (FAccess <> Value) then
  begin
    FAccess := Value;
    Update;
  end;
end;

procedure TZYInstructionFlagsInfo.SetFlagOperation(Index: Integer; Value: TZYFlagOperation);
begin
  Assert((Index >= Low(FFlags)) and (Index <= High(FFlags)));
  if (FFlags[Index] <> Value) then
  begin
    FFlags[Index] := Value;
    Update;
  end;
end;

procedure TZYInstructionFlagsInfo.UpdateManagedOperand;
var
  I: Integer;
  R, W: Boolean;
begin
  R := false;
  W := false;
  for I := Low(FFlags) to High(FFlags) do
  begin
    if (FFlags[I] = foTested) then
    begin
      R := true;
    end else
    if (FFlags[I] in [foModified, foSet0, foSet1, foUndefined]) then
    begin
      W := true;
    end;
  end;
  if (R or W) then
  begin
    FManagedOperand.OperandType := optImplicitReg;
    FManagedOperand.Visible := false;
  end else
  begin
    FManagedOperand.OperandType := optUnused;
    Exit;
  end;
  if (R and W) then
  begin
    FManagedOperand.Action := opaReadWrite;
    if (FAccess = faMayWrite) then
    begin
      FManagedOperand.Action := opaReadCondWrite;
    end;
  end else
  if (R) then
  begin
    FManagedOperand.Action := opaRead;
  end else
  if (W) then
  begin
    FManagedOperand.Action := opaWrite;
    if (FAccess = faMayWrite) then
    begin
      FManagedOperand.Action := opaCondWrite;
    end;
  end;
end;
{$ENDREGION}

{$REGION 'Class: TZYInstructionVEXInfo'}
procedure TZYInstructionVEXInfo.AssignTo(Dest: TPersistent);
var
  D: TZYInstructionVEXInfo;
begin
  if (Dest is TZYInstructionVEXInfo) then
  begin
    D := TZYInstructionVEXInfo(Dest);
    D.BeginUpdate;
    try
      D.SetStaticBroadcast(FStaticBroadcast);
      // TODO:
    finally
      D.EndUpdate;
    end;
  end else inherited;
end;

constructor TZYInstructionVEXInfo.Create(Definition: TZYInstructionDefinition);
begin
  inherited Create(Definition);

end;

destructor TZYInstructionVEXInfo.Destroy;
begin

  inherited;
end;

function TZYInstructionVEXInfo.Equals(Obj: TObject): Boolean;
var
  O: TZYInstructionVEXInfo;
begin
  Result := false;
  if (Obj is TZYInstructionVEXInfo) then
  begin
    O := TZYInstructionVEXInfo(Obj);
    Result :=
      (O.FStaticBroadcast = FStaticBroadcast);
  end;
end;

procedure TZYInstructionVEXInfo.LoadFromJSON(const JSON: IJSONObjectReader);
begin
  BeginUpdate;
  try
    SetStaticBroadcast(JSON.Reader.ReadEnum<TZYStaticBroadcast>(
      'static_broadcast', sbcNone, TZYStaticBroadcast.JSONStrings));
  finally
    EndUpdate;
  end;
end;

procedure TZYInstructionVEXInfo.SaveToJSON(const JSON: IJSONObjectWriter);
begin
  if (FStaticBroadcast <> sbcNone) then JSON.Writer.WriteEnum(
    'static_broadcast', FStaticBroadcast, TZYStaticBroadcast.JSONStrings);
end;

procedure TZYInstructionVEXInfo.SetStaticBroadcast(Value: TZYStaticBroadcast);
begin
  if (FStaticBroadcast <> Value) then
  begin
    FStaticBroadcast := Value;
    Update;
  end;
end;
{$ENDREGION}

{$REGION 'Class: TZYInstructionEVEXInfo'}
procedure TZYInstructionEVEXInfo.AssignTo(Dest: TPersistent);
var
  D: TZYInstructionEVEXInfo;
begin
  if (Dest is TZYInstructionEVEXInfo) then
  begin
    D := TZYInstructionEVEXInfo(Dest);
    D.BeginUpdate;
    try
      D.SetVectorLength(FVectorLength);
      D.SetFunctionality(FFunctionality);
      D.SetMaskMode(FMaskMode);
      D.SetMaskFlags(FMaskFlags);
      D.SetTupleType(FTupleType);
      D.SetElementSize(FElementSize);
      D.SetStaticBroadcast(FStaticBroadcast);
      // TODO:
    finally
      D.EndUpdate;
    end;
  end else inherited;
end;

constructor TZYInstructionEVEXInfo.Create(Definition: TZYInstructionDefinition);
begin
  inherited Create(Definition);
  FMaskFlags := [mfAcceptsZeroMask];
end;

destructor TZYInstructionEVEXInfo.Destroy;
begin

  inherited;
end;

function TZYInstructionEVEXInfo.Equals(Obj: TObject): Boolean;
var
  O: TZYInstructionEVEXInfo;
begin
  Result := false;
  if (Obj is TZYInstructionEVEXInfo) then
  begin
    O := TZYInstructionEVEXInfo(Obj);
    Result :=
      (O.FVectorLength = FVectorLength) and
      (O.FFunctionality = FFunctionality) and
      (O.FMaskMode = FMaskMode) and
      (O.FMaskFlags = FMaskFlags) and
      (O.FTupleType = FTupleType) and
      (O.FElementSize = FElementSize) and
      (O.FStaticBroadcast = FStaticBroadcast);
  end;
end;

procedure TZYInstructionEVEXInfo.LoadFromJSON(const JSON: IJSONObjectReader);
begin
  BeginUpdate;
  try
    SetVectorLength(JSON.Reader.ReadEnum<TZYVectorLength>(
      'vector_length', vlDefault, TZYVectorLength.JSONStrings));
    SetFunctionality(JSON.Reader.ReadEnum<TZYEVEXFunctionality>(
      'functionality', evInvalid, TZYEVEXFunctionality.JSONStrings));
    SetMaskMode(JSON.Reader.ReadEnum<TZYMEVEXMaskMode>(
      'mask_mode', mmMaskInvalid, TZYMEVEXMaskMode.JSONStrings));
    SetMaskFlags(JSON.Reader.ReadSet<TZYEVEXMaskFlags>(
      'mask_flags', [mfAcceptsZeroMask], TZYEVEXMaskFlag.JSONStrings));
    SetTupleType(JSON.Reader.ReadEnum<TZYEVEXTupleType>(
      'tuple_type', ttInvalid, TZYEVEXTupleType.JSONStrings));
    SetElementSize(JSON.Reader.ReadEnum<TZYEVEXElementSize>(
      'element_size', esInvalid, TZYEVEXElementSize.JSONStrings));
    SetStaticBroadcast(JSON.Reader.ReadEnum<TZYStaticBroadcast>(
      'static_broadcast', sbcNone, TZYStaticBroadcast.JSONStrings));
  finally
    EndUpdate;
  end;
end;

procedure TZYInstructionEVEXInfo.SaveToJSON(const JSON: IJSONObjectWriter);
begin
  if (FVectorLength <> vlDefault) then JSON.Writer.WriteEnum(
    'vector_length', FVectorLength, TZYVectorLength.JSONStrings);
  if (FFunctionality <> evInvalid) then JSON.Writer.WriteEnum(
    'functionality', FFunctionality, TZYEVEXFunctionality.JSONStrings);
  if (FMaskMode <> mmMaskInvalid) then JSON.Writer.WriteEnum(
    'mask_mode', FMaskMode, TZYMEVEXMaskMode.JSONStrings);
  if (FMaskFlags <> [mfAcceptsZeroMask]) then JSON.Writer.WriteSet(
    'mask_flags', FMaskFlags, TZYEVEXMaskFlag.JSONStrings);
  if (FTupleType <> ttInvalid) then JSON.Writer.WriteEnum(
    'tuple_type', FTupleType, TZYEVEXTupleType.JSONStrings);
  if (FElementSize <> esInvalid) then JSON.Writer.WriteEnum(
    'element_size', FElementSize, TZYEVEXElementSize.JSONStrings);
  if (FStaticBroadcast <> sbcNone) then JSON.Writer.WriteEnum(
    'static_broadcast', FStaticBroadcast, TZYStaticBroadcast.JSONStrings);
end;

procedure TZYInstructionEVEXInfo.SetElementSize(Value: TZYEVEXElementSize);
begin
  if (FElementSize <> Value) then
  begin
    FElementSize := Value;
    Update;
  end;
end;

procedure TZYInstructionEVEXInfo.SetFunctionality(Value: TZYEVEXFunctionality);
begin
  if (FFunctionality <> Value) then
  begin
    FFunctionality := Value;
    Update;
  end;
end;

procedure TZYInstructionEVEXInfo.SetMaskFlags(Value: TZYEVEXMaskFlags);
begin
  if (FMaskFlags <> Value) then
  begin
    FMaskFlags := Value;
    Update;
  end;
end;

procedure TZYInstructionEVEXInfo.SetMaskMode(Value: TZYMEVEXMaskMode);
begin
  if (FMaskMode <> Value) then
  begin
    FMaskMode := Value;
    Update;
  end;
end;

procedure TZYInstructionEVEXInfo.SetStaticBroadcast(Value: TZYStaticBroadcast);
begin
  if (FStaticBroadcast <> Value) then
  begin
    FStaticBroadcast := Value;
    Update;
  end;
end;

procedure TZYInstructionEVEXInfo.SetTupleType(Value: TZYEVEXTupleType);
begin
  if (FTupleType <> Value) then
  begin
    FTupleType := Value;
    Update;
  end;
end;

procedure TZYInstructionEVEXInfo.SetVectorLength(Value: TZYVectorLength);
begin
  if (FVectorLength <> Value) then
  begin
    FVectorLength := Value;
    Update;
  end;
end;
{$ENDREGION}

{$REGION 'Class: TZYInstructionMVEXInfo'}
procedure TZYInstructionMVEXInfo.AssignTo(Dest: TPersistent);
var
  D: TZYInstructionMVEXInfo;
begin
  if (Dest is TZYInstructionMVEXInfo) then
  begin
    D := TZYInstructionMVEXInfo(Dest);
    D.BeginUpdate;
    try
      D.SetFunctionality(FFunctionality);
      D.SetMaskMode(FMaskMode);
      D.SetHasElementGranularity(FHasElementGranularity);
      D.SetStaticBroadcast(FStaticBroadcast);
      // TODO:
    finally
      D.EndUpdate;
    end;
  end else inherited;
end;

constructor TZYInstructionMVEXInfo.Create(Definition: TZYInstructionDefinition);
begin
  inherited Create(Definition);

end;

destructor TZYInstructionMVEXInfo.Destroy;
begin

  inherited;
end;

function TZYInstructionMVEXInfo.Equals(Obj: TObject): Boolean;
var
  O: TZYInstructionMVEXInfo;
begin
  Result := false;
  if (Obj is TZYInstructionMVEXInfo) then
  begin
    O := TZYInstructionMVEXInfo(Obj);
    Result :=
      (O.FFunctionality = FFunctionality) and
      (O.FMaskMode = FMaskMode) and
      (O.FHasElementGranularity = FHasElementGranularity) and
      (O.FStaticBroadcast = FStaticBroadcast);
  end;
end;

function TZYInstructionMVEXInfo.GetVectorLength: TZYVectorLength;
begin
  Result := vlFixed512;
end;

procedure TZYInstructionMVEXInfo.LoadFromJSON(const JSON: IJSONObjectReader);
begin
  BeginUpdate;
  try
    SetFunctionality(JSON.Reader.ReadEnum<TZYMVEXFunctionality>(
      'functionality', mvIgnored, TZYMVEXFunctionality.JSONStrings));
    SetMaskMode(JSON.Reader.ReadEnum<TZYMEVEXMaskMode>(
      'mask_mode', mmMaskInvalid, TZYMEVEXMaskMode.JSONStrings));
    SetHasElementGranularity(JSON.ReadBoolean(
      'element_granularity', false));
    SetStaticBroadcast(JSON.Reader.ReadEnum<TZYStaticBroadcast>(
      'static_broadcast', sbcNone, TZYStaticBroadcast.JSONStrings));
  finally
    EndUpdate;
  end;
end;

procedure TZYInstructionMVEXInfo.SaveToJSON(const JSON: IJSONObjectWriter);
begin
  if (FFunctionality <> mvIgnored) then JSON.Writer.WriteEnum(
    'functionality', FFunctionality, TZYMVEXFunctionality.JSONStrings);
  if (FMaskMode <> mmMaskInvalid) then JSON.Writer.WriteEnum(
    'mask_mode', FMaskMode, TZYMEVEXMaskMode.JSONStrings);
  if (FHasElementGranularity) then JSON.WriteBoolean(
    'element_granularity', FHasElementGranularity);
  if (FStaticBroadcast <> sbcNone) then JSON.Writer.WriteEnum(
    'static_broadcast', FStaticBroadcast, TZYStaticBroadcast.JSONStrings);
end;

procedure TZYInstructionMVEXInfo.SetFunctionality(Value: TZYMVEXFunctionality);
begin
  if (FFunctionality <> Value) then
  begin
    FFunctionality := Value;
    Update;
  end;
end;

procedure TZYInstructionMVEXInfo.SetHasElementGranularity(Value: Boolean);
begin
  if (FHasElementGranularity <> Value) then
  begin
    FHasElementGranularity := Value;
    Update;
  end;
end;

procedure TZYInstructionMVEXInfo.SetStaticBroadcast(Value: TZYStaticBroadcast);
begin
  if (FStaticBroadcast <> Value) then
  begin
    FStaticBroadcast := Value;
    Update;
  end;
end;

procedure TZYInstructionMVEXInfo.SetMaskMode(Value: TZYMEVEXMaskMode);
begin
  if (FMaskMode <> Value) then
  begin
    FMaskMode := Value;
    Update;
  end;
end;
{$ENDREGION}

{$REGION 'Class: TZYInstructionPartInfo'}
constructor TZYInstructionPartInfo.Create;
begin
  inherited Create;
  FParts := [ipOpcode];
end;

function TZYInstructionPartInfo.Equals(Obj: TObject): Boolean;
var
  O: TZYInstructionPartInfo;
begin
  Result := false;
  if (Obj is TZYInstructionPartInfo) then
  begin
    O := TZYInstructionPartInfo(Obj);
    Result :=
      (FParts = O.FParts) and
      (FDisplacement.Width16 = O.FDisplacement.Width16) and
      (FDisplacement.Width32 = O.FDisplacement.Width32) and
      (FDisplacement.Width64 = O.FDisplacement.Width64) and
      (FImmediates[0].Width16 = O.FImmediates[0].Width16) and
      (FImmediates[0].Width32 = O.FImmediates[0].Width32) and
      (FImmediates[0].Width64 = O.FImmediates[0].Width64) and
      (FImmediates[0].IsSigned = O.FImmediates[0].IsSigned) and
      (FImmediates[0].IsRelative = O.FImmediates[0].IsRelative) and
      (FImmediates[1].Width16 = O.FImmediates[1].Width16) and
      (FImmediates[1].Width32 = O.FImmediates[1].Width32) and
      (FImmediates[1].Width64 = O.FImmediates[1].Width64) and
      (FImmediates[1].IsSigned = O.FImmediates[1].IsSigned) and
      (FImmediates[1].IsRelative = O.FImmediates[1].IsRelative);
  end;
end;

function TZYInstructionPartInfo.GetImmediate(Index: Integer): TZYInstructionPartImmediate;
begin
  Result := FImmediates[Index];
end;

procedure TZYInstructionPartInfo.Update(Definition: TZYInstructionDefinition);

procedure SetDisplacement(Width16, Width32, Width64: Integer);
begin
  Assert(not ((ipDisplacement in FParts) or (ipImmediate1 in FParts)));
  Include(FParts, ipDisplacement);
  FDisplacement.Width16 := Width16;
  FDisplacement.Width32 := Width32;
  FDisplacement.Width64 := Width64;
end;

procedure SetImmediate(var Index: Integer; Width16, Width32, Width64: Integer;
  IsSigned, IsRelative: Boolean);
begin
  Assert((Index = 0) or (not (ipDisplacement in FParts)));
  case Index of
    0: Include(FParts, ipImmediate0);
    1: Include(FParts, ipImmediate1) else
      Assert(false); // TODO: Validator
  end;
  FImmediates[Index].Width16 := Width16;
  FImmediates[Index].Width32 := Width32;
  FImmediates[Index].Width64 := Width64;
  FImmediates[Index].IsSigned := IsSigned;
  FImmediates[Index].IsRelative := IsRelative;
  Inc(Index);
end;

var
  I, N: Integer;
  O: TZYInstructionOperand;
  HasIS4: Boolean;
begin
  FParts := [ipOpcode];
  FillChar(FDisplacement, SizeOf(FDisplacement), #0);
  FillChar(FImmediates[0], Length(FImmediates) * SizeOf(FImmediates[0]), #0);
  if (Definition.Filters.ModrmMod <> mdPlaceholder) or
     (Definition.Filters.ModrmReg <> rgPlaceholder) or
     (Definition.Filters.ModrmRm  <> rmPlaceholder) then
  begin
    Include(FParts, ipModrm);
  end;
  N := 0;
  HasIS4 := false;
  for I := 0 to Definition.Operands.NumberOfUsedOperands - 1 do
  begin
    O := Definition.Operands.Items[I];
    if (O.OperandType = optPTR) then
    begin
      Assert(I = 0);
      SetImmediate(N, 16, 32, 32, true , true );
      SetImmediate(N, 16, 16, 16, false, false);
      Break;
    end;
    case O.Encoding of
      opeModrmReg    ,
      opeModrmRm     : Include(FParts, ipModrm);
      opeIS4         :
        begin
          if (not HasIS4) then
          begin
            HasIS4 := true;
            SetImmediate(N,  8,  8,  8, false, false);
          end;
        end;
      opeDisp8       : SetDisplacement(8,  8,  8);
      opeDisp16      : SetDisplacement(16, 16, 16);
      opeDisp32      : SetDisplacement(32, 32, 32);
      opeDisp64      : SetDisplacement(64, 64, 64);
      opeDisp16_32_64: SetDisplacement(16, 32, 64);
      opeDisp32_32_64: SetDisplacement(32, 32, 64);
      opeDisp16_32_32: SetDisplacement(16, 32, 32);
      opeUImm8       : SetImmediate(N,  8,  8,  8, false, false);
      opeUImm16      : SetImmediate(N, 16, 16, 16, false, false);
      opeUImm32      : SetImmediate(N, 32, 32, 32, false, false);
      opeUImm64      : SetImmediate(N, 64, 64, 64, false, false);
      opeUImm16_32_64: SetImmediate(N, 16, 32, 64, false, false);
      opeUImm32_32_64: SetImmediate(N, 32, 32, 64, false, false);
      opeUImm16_32_32: SetImmediate(N, 16, 32, 32, false, false);
      opeSImm8       : SetImmediate(N,  8,  8,  8, true , false);
      opeSImm16      : SetImmediate(N, 16, 16, 16, true , false);
      opeSImm32      : SetImmediate(N, 32, 32, 32, true , false);
      opeSImm64      : SetImmediate(N, 64, 64, 64, true , false);
      opeSImm16_32_64: SetImmediate(N, 16, 32, 64, true , false);
      opeSImm32_32_64: SetImmediate(N, 32, 32, 64, true , false);
      opeSImm16_32_32: SetImmediate(N, 16, 32, 32, true , false);
      opeJImm8       : SetImmediate(N,  8,  8,  8, true , true );
      opeJImm16      : SetImmediate(N, 16, 16, 16, true , true );
      opeJImm32      : SetImmediate(N, 32, 32, 32, true , true );
      opeJImm64      : SetImmediate(N, 64, 64, 64, true , true );
      opeJImm16_32_64: SetImmediate(N, 16, 32, 64, true , true );
      opeJImm32_32_64: SetImmediate(N, 32, 32, 64, true , true );
      opeJImm16_32_32: SetImmediate(N, 16, 32, 32, true , true );
    end;
  end;
  if (dfForceRegForm in Definition.Flags) then
  begin
    Assert(ipModrm in FParts);
    Include(FParts, ipForceRegForm);
  end;
end;
{$ENDREGION}

{$REGION 'Class: TZYInstructionDefinition'}
procedure TZYInstructionDefinition.AssignTo(Dest: TPersistent);
var
  D: TZYInstructionDefinition;
begin
  if (Dest is TZYInstructionDefinition) then
  begin
    D := TZYInstructionDefinition(Dest);
    D.BeginUpdate;
    try
      D.SetMnemonic(FMnemonic);
      D.SetEncoding(FEncoding);
      D.SetOpcodeMap(FOpcodeMap);
      D.SetOpcode(Opcode);
      D.FFilters.Assign(FFilters);
      D.FOperands.Assign(FOperands);
      D.SetOperandSizeMap(FOperandSizeMap);
      D.SetPrivilegeLevel(FPrivilegeLevel);
      D.SetFlags(FFlags);
      D.FMetaInfo.Assign(FMetaInfo);
      D.SetPrefixFlags(FPrefixFlags);
      D.SetExceptionClass(FExceptionClass);
      D.FAffectedFlags.Assign(FAffectedFlags);
      D.FVEXInfo.Assign(FVEXInfo);
      D.FEVEXInfo.Assign(FEVEXInfo);
      D.FMVEXInfo.Assign(FMVEXInfo);

      D.SetComment(FComment);
    finally
      D.EndUpdate;
    end;
  end else inherited;
end;

procedure TZYInstructionDefinition.BeginUpdate;
begin
  Inc(FUpdateCount);
end;

procedure TZYInstructionDefinition.ChangedPattern;
begin
  // The instruction pattern got changed and the instruction-definition needs to get re-inserted
  // into the tree-structure
  UpdateSpecialFilters;
  if (FInserted) then
  begin
    FEditor.InsertDefinition(Self);
    Assert(FParent <> nil);
  end else
  begin
    if Assigned(FParent) then
    begin
      FEditor.RemoveDefinition(Self);
      Assert(FParent = nil);
    end;
  end;
  UpdateConflictState;
  FInstructionParts.Update(Self);
end;

procedure TZYInstructionDefinition.ChangedProperties;
var
  B: Boolean;
begin
  // One or more non-pattern properties of the instruction-definition got changed
  B := FHasConflicts;
  UpdateConflictState;
  if (FHasConflicts = B) then
  begin
    // If the conflict state has been changed, `DefinitionChanged` gets called by the
    // @c UpdateConflictState method.
    FEditor.DefinitionChanged(Self);
  end;
  FInstructionParts.Update(Self);
end;

procedure TZYInstructionDefinition.CheckMnemonic(const Value: String);
var
  B: Boolean;
  I: Integer;
begin
  B := true;
  for I := 1 to Length(Value) do
  begin
    if (not CharInSet(Value[I], ['a'..'z', 'A'..'Z', '0'..'9', '_'])) then
    begin
      B := false;
      Break;
    end;
  end;
  if (not B) or (Value = '') then
  begin
    raise EZYDefinitionPropertyException.Create('Invalid mnemonic string.');
  end;
end;

function TZYInstructionDefinition.CompareTo(Other: TZYInstructionDefinition): Integer;
begin
  Result := TComparator.Init
    .Comparing(Mnemonic, Other.Mnemonic)
    .ComparingGeneric(Self.Encoding, Other.Encoding)
    .ComparingGeneric(Self.OpcodeMap, Other.OpcodeMap)
    .Comparing(Self.Opcode, Other.Opcode)
    .ComparingGeneric(Self.Filters.MandatoryPrefix, Other.Filters.MandatoryPrefix)
    .Comparing<TZYInstructionDefinition>(Self, Other,
      function(const Left, Right: TZYInstructionDefinition): Integer
      var
        F: TZYInstructionFilterClass;
      begin
        Result := 0;
        F := ifcMode;
        while (Result = 0) and (F <= High(TZYInstructionFilterClass)) do
        begin
          if (F <> ifcMandatoryPrefix) then
          begin
            Result := Left.FilterIndex[F] - Right.FilterIndex[F];
          end;
          Inc(F);
        end;
      end)
    .Compare;
  // TODO: Compare missing values
end;

constructor TZYInstructionDefinition.Create(Editor: TZYInstructionEditor; const Mnemonic: String);
begin
  inherited Create;
  FEditor := Editor;
  FInstructionParts := TZYInstructionPartInfo.Create;

  FFilters := TZYInstructionFilters.Create(Self);
  FOperands := TZYInstructionOperands.Create(Self);
  FMetaInfo := TZYInstructionMetaInfo.Create(Self);
  FAffectedFlags := TZYInstructionFlagsInfo.Create(Self);
  FVEXInfo := TZYInstructionVEXInfo.Create(Self);
  FEVEXInfo := TZYInstructionEVEXInfo.Create(Self);
  FMVEXInfo := TZYInstructionMVEXInfo.Create(Self);

  CheckMnemonic(Mnemonic);
  FMnemonic := Mnemonic;
  FPrivilegeLevel := 3;

  // Insert definition into the definition list. This method does NOT insert the definition into
  // the table-structure
  FEditor.RegisterDefinition(Self);

  FEditor.RaiseDefinitionEvent(TZYInstructionEditor.CREATED, Self);
end;

destructor TZYInstructionDefinition.Destroy;
begin
  // Remove definition from the tree-structure
  if Assigned(FParent) then
  begin
    FEditor.RemoveDefinition(Self);
  end;
  // Remove definition from the definition list
  FEditor.UnregisterDefinition(Self);

  FEditor.RaiseDefinitionEvent(TZYInstructionEditor.DESTROYED, Self);

  FInstructionParts.Free;

  FFilters.Free;
  FOperands.Free;
  FMetaInfo.Free;
  FAffectedFlags.Free;
  FVEXInfo.Free;
  FEVEXInfo.Free;
  FMVEXInfo.Free;
  inherited;
end;

procedure TZYInstructionDefinition.EndUpdate;
begin
  if (FUpdateCount = 0) then
  begin
    raise Exception.Create('Invalid action');
  end;
  Dec(FUpdateCount);
  if (FUpdateCount = 0) then
  begin
    if (FNeedsUpdateProperties) then
    begin
      FNeedsUpdateProperties := false;
      ChangedProperties;
    end;
    if (FNeedsUpdatePattern) then
    begin
      FNeedsUpdatePattern := false;
      ChangedPattern;
    end;
  end;
end;

function TZYInstructionDefinition.HasEqualPattern(Definition: TZYInstructionDefinition): Boolean;
begin
  Result :=
    (FEncoding = Definition.FEncoding) and
    (FOpcodeMap = Definition.FOpcodeMap) and
    (Opcode = Definition.Opcode) and
    (FFilters.Equals(Definition.FFilters));
end;

function TZYInstructionDefinition.HasEqualProperties(Definition: TZYInstructionDefinition): Boolean;
begin
  Result :=
    (FMnemonic = Definition.FMnemonic) and
    (FOperands.Equals(Definition.FOperands)) and
    (FOperandSizeMap = Definition.FOperandSizeMap) and
    (FPrivilegeLevel = Definition.FPrivilegeLevel) and
    (FFlags = Definition.FFlags) and
    (FMetaInfo.Equals(Definition.FMetaInfo)) and
    (FPrefixFlags = Definition.FPrefixFlags) and
    (FExceptionClass = Definition.FExceptionClass) and
    (FAffectedFlags.Equals(Definition.FAffectedFlags)) and
    (FVEXInfo.Equals(Definition.FVEXInfo)) and
    (FEVEXInfo.Equals(Definition.FEVEXInfo)) and
    (FMVEXInfo.Equals(Definition.FMVEXInfo)) and
    // TODO:
    (FComment = Definition.FComment);
end;

function TZYInstructionDefinition.Equals(Obj: TObject): Boolean;
var
  O: TZYInstructionDefinition;
begin
  Result := false;
  if (Obj is TZYInstructionDefinition) then
  begin
    O := TZYInstructionDefinition(Obj);
    Result := HasEqualPattern(O) and HasEqualProperties(O);
  end;
end;

procedure TZYInstructionDefinition.GetConflictMessages(var ConflictMessages: TArray<String>);
begin
  if (FHasConflicts) then
  begin
    TZYDefinitionValidator.Validate(Self, ConflictMessages);
  end;
end;

function TZYInstructionDefinition.GetFilterIndex(FilterClass: TZYInstructionFilterClass): Integer;
begin
  Assert((FilterClass >= Low(TZYInstructionFilterClass)) and
    (FilterClass <= High(TZYInstructionFilterClass)));
  Result := FFilterIndex[FilterClass];
end;

function TZYInstructionDefinition.GetOpcode: TZYOpcode;
begin
  Result := GetFilterIndex(ifcOpcode);
end;

procedure TZYInstructionDefinition.Insert;
begin
  FInserted := true;
  UpdatePattern;
end;

procedure TZYInstructionDefinition.LoadFromJSON(const JSON: IJSONObjectReader);
var
  I: Integer;
  S: String;
begin
  BeginUpdate;
  try
    SetMnemonic(JSON.ReadString('mnemonic'));
    SetEncoding(JSON.Reader.ReadEnum<TZYInstructionEncoding>(
      'encoding', iencDefault, TZYEnumInstructionEncoding.JSONStrings));
    SetOpcodeMap(JSON.Reader.ReadEnum<TZYOpcodeMap>(
      'opcode_map', omapDefault, TZYEnumOpcodeMap.JSONStrings));
    if (JSON.ValueType('opcode') = jsonInteger) then
    begin
      I := JSON.ReadInteger('opcode');
    end else
    begin
      S := JSON.ReadString('opcode');
      if (not TryStrToInt('$' + S, I)) then
      begin
        I := -1;
      end;
    end;
    if (I < 0) or (I > 255) then
    begin
      raise EZYDefinitionPropertyException.Create(
        'The "opcode" field does not contain a valid opcode value.');
    end;
    SetOpcode(I);
    if (JSON.ValueExists('filters')) then
    begin
      FFilters.LoadFromJSON(JSON.ReadObject('filters'));
    end;
    if (JSON.ValueExists('operands')) then
    begin
      FOperands.LoadFromJSON(JSON.ReadArray('operands'));
    end;
    SetOperandSizeMap(JSON.Reader.ReadEnum<TZYOperandSizeMap>(
      'opsize_map', osmDefault, TZYOperandSizeMap.JSONStrings));
    SetPrivilegeLevel(JSON.ReadInteger('cpl', 3));
    SetFlags(JSON.Reader.ReadSet<TZYDefinitionFlags>(
      'flags', [], TZYEnumDefinitionFlag.JSONStrings));
    if (JSON.ValueExists('meta_info')) then
    begin
      FMetaInfo.LoadFromJSON(JSON.ReadObject('meta_info'));
    end;
    SetPrefixFlags(JSON.Reader.ReadSet<TZYPrefixFlags>(
      'prefix_flags', [], TZYEnumPrefixFlag.JSONStrings));
    SetExceptionClass(JSON.Reader.ReadEnum<TZYExceptionClass>(
      'exception_class', ecNone, TZYExceptionClass.JSONStrings));
    if (JSON.ValueExists('affected_flags')) then
    begin
      FAffectedFlags.LoadFromJSON(JSON.ReadObject('affected_flags'));
    end;
    if (JSON.ValueExists('vex')) then
    begin
      FVEXInfo.LoadFromJSON(JSON.ReadObject('vex'));
    end;
    if (JSON.ValueExists('evex')) then
    begin
      FEVEXInfo.LoadFromJSON(JSON.ReadObject('evex'));
    end;
    if (JSON.ValueExists('mvex')) then
    begin
      FMVEXInfo.LoadFromJSON(JSON.ReadObject('mvex'));
    end;

    S := JSON.ReadString('comment', '');
    if (S <> '') then
    begin
      SetComment(TNetEncoding.Base64.Decode(S));
    end;
  finally
    EndUpdate;
  end;
end;

procedure TZYInstructionDefinition.Remove;
begin
  FInserted := false;
  UpdatePattern;
end;

procedure TZYInstructionDefinition.SaveToJSON(const JSON: IJSONObjectWriter);
begin
  JSON.WriteString('mnemonic', FMnemonic);
  if (FEncoding <> iencDefault) then JSON.Writer.WriteEnum(
    'encoding', FEncoding, TZYEnumInstructionEncoding.JSONStrings);
  if (FOpcodeMap <> omapDefault) then JSON.Writer.WriteEnum(
    'opcode_map', FOpcodeMap, TZYEnumOpcodeMap.JSONStrings);
  JSON.WriteString('opcode', IntToHex(Opcode, 2));
  JSON.WriteObject('filters',
    function(JSON: TJSONObjectWriter): Boolean
    begin
      FFilters.SaveToJSON(JSON);
      Result := (JSON.InnerObject.Count > 0);
    end);
  JSON.WriteArray('operands',
    function(JSON: TJSONArrayWriter): Boolean
    begin
      FOperands.SaveToJSON(JSON);
      Result := (JSON.InnerObject.Count > 0);
    end);
  if (FOperandSizeMap <> osmDefault) then JSON.Writer.WriteEnum(
    'opsize_map', FOperandSizeMap, TZYOperandSizeMap.JSONStrings);
  if (FPrivilegeLevel <> 3) then JSON.WriteInteger(
    'cpl', FPrivilegeLevel);
  if (FFlags <> []) then JSON.Writer.WriteSet(
    'flags', FFlags, TZYEnumDefinitionFlag.JSONStrings);
  JSON.WriteObject('meta_info',
    function(JSON: TJSONObjectWriter): Boolean
    begin
      FMetaInfo.SaveToJSON(JSON);
      Result := (JSON.InnerObject.Count > 0);
    end);
  if (FPrefixFlags <> []) then JSON.Writer.WriteSet(
    'prefix_flags', FPrefixFlags, TZYEnumPrefixFlag.JSONStrings);
  if (FExceptionClass <> ecNone) then JSON.Writer.WriteEnum(
    'exception_class', FExceptionClass, TZYExceptionClass.JSONStrings);
  JSON.WriteObject('affected_flags',
    function(JSON: TJSONObjectWriter): Boolean
    begin
      FAffectedFlags.SaveToJSON(JSON);
      Result := (JSON.InnerObject.Count > 0);
    end);
  JSON.WriteObject('vex',
    function(JSON: TJSONObjectWriter): Boolean
    begin
      FVEXInfo.SaveToJSON(JSON);
      Result := (JSON.InnerObject.Count > 0);
    end);
  JSON.WriteObject('evex',
    function(JSON: TJSONObjectWriter): Boolean
    begin
      FEVEXInfo.SaveToJSON(JSON);
      Result := (JSON.InnerObject.Count > 0);
    end);
  JSON.WriteObject('mvex',
    function(JSON: TJSONObjectWriter): Boolean
    begin
      FMVEXInfo.SaveToJSON(JSON);
      Result := (JSON.InnerObject.Count > 0);
    end);

  if (FComment <> '') then JSON.WriteString('comment',
    StringReplace(TNetEncoding.Base64.Encode(FComment), sLineBreak, '', [rfReplaceAll]));
end;

procedure TZYInstructionDefinition.SetComment(const Value: String);
begin
  if (FComment <> Value) then
  begin
    FComment := Value;
    UpdateProperties;
  end;
end;

procedure TZYInstructionDefinition.SetEncoding(Value: TZYInstructionEncoding);
begin
  if (FEncoding <> Value) then
  begin
    BeginUpdate;
    try
      case Value of
        iencDEFAULT:
          if (not (FOpcodeMap in [omapDefault, omap0F, omap0F38, omap0F3A])) then
          begin
            FOpcodeMap := omapDefault;
          end;
        iencVEX,
        iencEVEX,
        iencMVEX:
          begin
            if (not (FOpcodeMap in [omapDefault, omap0F, omap0F38, omap0F3A])) then
            begin
              FOpcodeMap := omapDefault;
            end;
            if (FFilters.MandatoryPrefix = mpPlaceholder) then
            begin
              FFilters.MandatoryPrefix := mpNone;
            end;
          end;
        ienc3DNOW:
          if (not (FOpcodeMap in [omap0F0F])) then
          begin
            FOpcodeMap := omap0F0F;
          end;
        iencXOP:
          if (not (FOpcodeMap in [omapXOP8, omapXOP9, omapXOPA])) then
          begin
            FOpcodeMap := omapXOP8;
            FFilters.MandatoryPrefix := mpNone;
          end;
      end;
      FEncoding := Value;
      UpdatePattern;
    finally
      EndUpdate;
    end;
  end;
end;

procedure TZYInstructionDefinition.SetExceptionClass(Value: TZYExceptionClass);
begin
  if (FExceptionClass <> Value) then
  begin
    FExceptionClass := Value;
    UpdateProperties;
  end;
end;

procedure TZYInstructionDefinition.SetFilterIndex(FilterClass: TZYInstructionFilterClass;
  Value: Integer);
begin
  Assert((FilterClass >= Low(TZYInstructionFilterClass)) and
    (FilterClass <= High(TZYInstructionFilterClass)));
  if (FFilterIndex[FilterClass] <> Value) then
  begin
    FFilterIndex[FilterClass] := Value;
    UpdatePattern;
  end;
end;

procedure TZYInstructionDefinition.SetFlags(Value: TZYDefinitionFlags);
begin
  if (FFlags <> Value) then
  begin
    FFlags := Value;
    UpdateProperties;
  end;
end;

procedure TZYInstructionDefinition.SetMnemonic(const Value: TZYInstructionMnemonic);
begin
  if (FMnemonic <> Value) then
  begin
    CheckMnemonic(Value);
    FMnemonic := LowerCase(Value);
    UpdateProperties;
  end;
end;

procedure TZYInstructionDefinition.SetOpcode(Value: TZYOpcode);
begin
  SetFilterIndex(ifcOpcode, Value);
end;

procedure TZYInstructionDefinition.SetOpcodeMap(Value: TZYOpcodeMap);
var
  B: Boolean;
begin
  if (FOpcodeMap <> Value) then
  begin
    B := false;
    case FEncoding of
      iencDEFAULT,
      iencVEX,
      iencEVEX,
      iencMVEX : B := (Value in [omapDefault, omap0F, omap0F38, omap0F3A]);
      ienc3DNOW: B := (Value in [omap0F0F]);
      iencXOP  : B := (Value in [omapXOP8, omapXOP9, omapXOPA]);
    end;
    if (not B) then
    begin
      raise EZYDefinitionPropertyException.CreateFmt(
        'The %s instruction encoding does not support the %s opcode map.', [
        TZYEnumInstructionEncoding.ZydisStrings[FEncoding],
        TZYEnumOpcodeMap.DisplayStrings[Value]]);
    end;
    FOpcodeMap := Value;
    UpdatePattern;
  end;
end;

procedure TZYInstructionDefinition.SetOperandSizeMap(Value: TZYOperandSizeMap);
begin
  if (FOperandSizeMap <> Value) then
  begin
    FOperandSizeMap := Value;
    UpdateProperties;
  end;
end;

procedure TZYInstructionDefinition.SetParent(const Value: TZYInstructionTreeNode);
begin
  // This method should ONLY be called by TZYInstructionTreeNode.InsertDefinition and
  // TZYInstructionTreeNode.RemoveDefinition
  if (Assigned(FParent)) then
  begin
    if (HasConflicts) then
    begin
      FParent.DecDefinitionConflictCount;
    end;
    FEditor.RaiseDefinitionEvent(TZYInstructionEditor.REMOVED, Self);
  end;
  FParent := Value;
  if (Assigned(Value)) then
  begin
    if (HasConflicts) then
    begin
      FParent.IncDefinitionConflictCount;
    end;
    FEditor.RaiseDefinitionEvent(TZYInstructionEditor.INSERTED, Self);
  end;
end;

procedure TZYInstructionDefinition.SetPrefixFlags(Value: TZYPrefixFlags);
begin
  if (FPrefixFlags <> Value) then
  begin
    FPrefixFlags := Value;
    UpdateProperties;
  end;
end;

procedure TZYInstructionDefinition.SetPrivilegeLevel(Value: TZYCPUPrivilegeLevel);
begin
  if (FPrivilegeLevel <> Value) then
  begin
    FPrivilegeLevel := Value;
    UpdateProperties;
  end;
end;

procedure TZYInstructionDefinition.UpdateConflictState;
var
  B: Boolean;
begin
  B := FHasConflicts;
  FHasConflicts := (dfForceConflict in FFlags) or (not TZYDefinitionValidator.Validate(Self));
  if (B <> FHasConflicts) then
  begin
    FEditor.DefinitionChanged(Self);
  end;
  if Assigned(FParent) then
  begin
    if (HasConflicts) then
    begin
      if (not B) then
      begin
        FParent.IncDefinitionConflictCount;
      end;
    end else
    begin
      if (B) then
      begin
        FParent.DecDefinitionConflictCount;
      end;
    end;
  end;
end;

procedure TZYInstructionDefinition.UpdatePattern;
begin
  if (FUpdateCount = 0) then
  begin
    ChangedPattern;
  end else
  begin
    FNeedsUpdatePattern := true;
  end;
end;

procedure TZYInstructionDefinition.UpdateProperties;
begin
  if (FUpdateCount = 0) then
  begin
    ChangedProperties;
  end else
  begin
    FNeedsUpdateProperties := true;
  end;
end;

procedure TZYInstructionDefinition.UpdateSpecialFilters;
var
  V: Integer;
begin
  // This function should only be called by @c ChangedPattern
  Assert(FUpdateCount = 0);
  BeginUpdate;
  try
    case FEncoding of
      iencDEFAULT,
      ienc3DNOW  :
        begin
          SetFilterIndex(ifcXOP  , 0);
          SetFilterIndex(ifcVEX  , 0);
          SetFilterIndex(ifcEMVEX, 0);
        end;
      iencXOP    :
        begin
          case FOpcodeMap of
            omapXOP8: SetFilterIndex(ifcXOP, 1);
            omapXOP9: SetFilterIndex(ifcXOP, 2);
            omapXOPA: SetFilterIndex(ifcXOP, 3) else
              Assert(false);
          end;
        end;
      iencVEX    :
        begin
          V := -1;
          case FFilters.MandatoryPrefix of
            mpNone: V := 0 * 4;
            mp66  : V := 1 * 4;
            mpF3  : V := 2 * 4;
            mpF2  : V := 3 * 4 else
              Assert(false);
          end;
          case FOpcodeMap of
            omapDEFAULT: ;
            omap0F     : V := V + 1;
            omap0F38   : V := V + 2;
            omap0F3A   : V := V + 3 else
              Assert(false);
          end;
          SetFilterIndex(ifcVEX, V + 1);
        end;
      iencEVEX   ,
      iencMVEX   :
        begin
          V := -1;
          case FFilters.MandatoryPrefix of
            mpNone: V := 0 * 4;
            mp66  : V := 1 * 4;
            mpF3  : V := 2 * 4;
            mpF2  : V := 3 * 4 else
              Assert(false);
          end;
          case FOpcodeMap of
            omapDEFAULT: ;
            omap0F     : V := V + 1;
            omap0F38   : V := V + 2;
            omap0F3A   : V := V + 3 else
              Assert(false);
          end;
          case FEncoding of
            iencEVEX: V := V + 1;
            iencMVEX: V := V + 17 else
              Assert(false);
          end;
          SetFilterIndex(ifcEMVEX, V);
        end;
    end;
  finally
    // Suppress redundant invokation of @c ChangedPattern
    FNeedsUpdatePattern := false;
    EndUpdate;
  end;
end;
{$ENDREGION}

{$REGION 'Class: TZYInstructionEditor'}
procedure TZYInstructionEditor.BeginUpdate;
begin
  Inc(FUpdateCount);
  if (FUpdateCount = 1) then
  begin
    if Assigned(FOnBeginUpdate) then
    begin
      FOnBeginUpdate(Self);
    end;
  end;
end;

constructor TZYInstructionEditor.Create;
begin
  inherited Create;
  FDefinitions := TList<TZYInstructionDefinition>.Create;
end;

function TZYInstructionEditor.CreateDefinition(const Mnemonic: String): TZYInstructionDefinition;
begin
  Result := TZYInstructionDefinition.Create(Self, Mnemonic);
end;

procedure TZYInstructionEditor.DefinitionChanged(Definition: TZYInstructionDefinition);
begin
  // Changes in property values might cause a change in the definition order. This method
  // re-inserts the definition to update the list
  if (not FDelayDefinitionRegistration) then
  begin
    UnregisterDefinition(Definition);
    RegisterDefinition(Definition);
  end;

  // Raise the `CHANGED` event
  RaiseDefinitionEvent(TZYInstructionEditor.CHANGED, Definition);
end;

destructor TZYInstructionEditor.Destroy;

procedure DestroyChildNodes(Node: TZYInstructionTreeNode);
var
  I: Integer;
  N: TZYInstructionTreeNode;
begin
  Assert(itnfIsStaticNode in Node.Flags);
  if (Node.ChildCount > 0) then
  begin
    for I := 0 to Node.ChildCapacity - 1 do
    begin
      if (Assigned(Node.Childs[I])) then
      begin
        DestroyChildNodes(Node.Childs[I]);
        N := Node.Childs[I];
        Node.SetChildItem(I, nil);
        N.Free;
      end;
    end;
  end;
end;

var
  I: Integer;
begin
  BeginUpdate;
  try
    if (Assigned(FDefinitions)) then
    begin
      FPreventDefinitionRemoval := true;
      for I := FDefinitions.Count - 1 downto 0 do
      begin
        FDefinitions[I].Free;
      end;
      FDefinitions.Free;
    end;
    if Assigned(FRootNode) then
    begin
      DestroyChildNodes(FRootNode);
      FRootNode.Free;
    end;
  finally
    EndUpdate;
  end;
end;

procedure TZYInstructionEditor.EndUpdate;
begin
  Assert(FUpdateCount > 0);
  Dec(FUpdateCount);
  if (FUpdateCount = 0) then
  begin
    if Assigned(FOnEndUpdate) then
    begin
      FOnEndUpdate(Self);
    end;
  end;
end;

function TZYInstructionEditor.GetDefinition(Index: Integer): TZYInstructionDefinition;
begin
  Assert((Index >= 0) and (Index < FDefinitions.Count));
  Result := FDefinitions[Index];
end;

function TZYInstructionEditor.GetDefinitionCount: Integer;
begin
  Result := FDefinitions.Count;
end;

function TZYInstructionEditor.GetDefinitionEvent(Index: Integer): TZYEditorDefinitionEvent;
begin
  Result := FDefinitionEvent[Index];
end;

function TZYInstructionEditor.GetDefinitionTopLevelFilter(
  Definition: TZYInstructionDefinition): TZYInstructionTreeNode;
begin
  Result := nil;
  case Definition.Encoding of
    iencDefault:
      begin
        case Definition.OpcodeMap of
          omapDefault:
            Result := FRootNode;
          omap0F:
            Result := FRootNode.Childs[$0F];
          omap0F38:
            Result := FRootNode.Childs[$0F].Childs[$38];
          omap0F3A:
            Result := FRootNode.Childs[$0F].Childs[$3A];
          omapXOP8,
          omapXOP9,
          omapXOPA:
            Assert(false);
        end;
      end;
    ienc3DNow: Result := FRootNode.Childs[$0F].Childs[$0F];
    iencXOP  : Result := FRootNode.Childs[$8F];
    iencVEX  : Result := FRootNode.Childs[$C4];
    iencEVEX,
    iencMVEX : Result := FRootNode.Childs[$62];
  end;
  Assert(Assigned(Result));
end;

function TZYInstructionEditor.GetFilterCount(FilterClass: TZYInstructionFilterClass): Integer;
begin
  Result := FFilterCount[FilterClass];
end;

function TZYInstructionEditor.GetNodeEvent(Index: Integer): TZYEditorNodeEvent;
begin
  Result := FNodeEvent[Index];
end;

procedure TZYInstructionEditor.InsertDefinition(Definition: TZYInstructionDefinition);
var
  N, T: TZYInstructionTreeNode;
  I, Index: Integer;
  FilterList: TZYInstructionFilterList;
  IsRequiredFilter: Boolean;
begin
  if (FDelayDefinitionRegistration) then
  begin
    RegisterDefinition(Definition);
  end;
  BeginUpdate;
  try
    // Remove the definition from its old position
    RemoveDefinition(Definition);

    // Skip all static filters. This code assumes that the parent of a static filter is always
    // another static filter.
    // There is no need to create a static filter as child of a non-static one at the moment.
    N := GetDefinitionToplevelFilter(Definition);
    Index := Definition.FilterIndex[N.FilterClass];
    while (Assigned(N.Childs[Index])) and (itnfIsStaticNode in N.Childs[Index].Flags) do
    begin
      N := N.Childs[Index];
      Index := Definition.FilterIndex[N.FilterClass];;
    end;

    // Create required filters
    FilterList := TZYInstructionFilterInfo.FilterOrder[Definition.Encoding];
    for I := Low(FilterList) to High(FilterList) do
    begin
      // Check if the current definition requires this filter
      case (TZYInstructionFilterInfo.Info[FilterList[I]].IsOptional) of
        false:
          begin
            // TODO: Always true?
            IsRequiredFilter := (Definition.FilterIndex[FilterList[I]] >= 0);
          end;
        true :
          begin
            IsRequiredFilter := (Definition.FilterIndex[FilterList[I]] >  0);
          end;
      end;

      Index := Definition.FilterIndex[N.FilterClass];

      // We have to enforce this filter, if a definition in the target-slot already requires the
      // same filter type
      if (not IsRequiredFilter) and
        (TZYInstructionFilterInfo.Info[FilterList[I]].IsOptional) and
        (Assigned(N.Childs[Index])) and (N.Childs[Index].FilterClass = FilterList[I]) then
      begin
        IsRequiredFilter := true;
      end;

      if (IsRequiredFilter) then
      begin
        // If the target slot is not occupied, just go ahead and create the new filter
        if (not Assigned(N.Childs[Index])) then
        begin
          N.CreateChildNodeAtIndex(Index, FilterList[I]);
        end;
        // If the target slot is occupied by a different filter type, we need to save the old
        // filter and insert it into our new one
        if (N.Childs[Index].FilterClass = FilterList[I]) then
        begin
          N := N.Childs[Index];
        end else
        begin
          T := N.Childs[Index];
          N.CreateChildNodeAtIndex(Index, FilterList[I]);
          N := N.Childs[Index];
          N.SetChildItem(0, T); // TODO: Always 0?
        end;
      end;
    end;

    // Create a definition-container and actually insert the definition
    Index := Definition.FilterIndex[N.FilterClass];
    if (not Assigned(N.Childs[Index])) then
    begin
      N.CreateChildNodeAtIndex(Index, ifcInvalid);
    end;
    N.Childs[Index].InsertDefinition(Definition);
  finally
    EndUpdate;
  end;
end;

procedure TZYInstructionEditor.LoadFromFile(const Filename: String);
var
  List: TStringList;
  JSON: TJSONValue;
  Reader: IJSONArrayReader;
begin
  List := TStringList.Create;
  try
    List.LoadFromFile(Filename);
    JSON := TJSONObject.ParseJSONValue(List.Text);
    try
      if (not Assigned(JSON)) or (TJSONHelper.ValueType(JSON) <> jsonArray) then
      begin
        raise Exception.Create('Could not parse JSON file.');
      end;
      Reader := TJSONArrayReader.Create(TJSONArray(JSON), 'DEFINITIONS');
      LoadFromJSON(Reader);
    finally
      JSON.Free;
    end;
  finally
    List.Free;
  end;
end;

procedure TZYInstructionEditor.LoadFromJSON(const JSON: IJSONArrayReader; DoReset: Boolean);
var
  I: Integer;
  D: TZYInstructionDefinition;
  E: EZYDefinitionJSONException;
begin
  BeginUpdate;
  try
    if (DoReset) then Reset;
    try
      if (Assigned(FOnWorkStart)) then
      begin
        FOnWorkStart(Self, 0, JSON.InnerObject.Count);
      end;
      try
        // Delays the definition-registration until the definition gets inserted for the first
        // time
        FDelayDefinitionRegistration := true;

        for I := 0 to JSON.InnerObject.Count - 1 do
        begin
          D := CreateDefinition('new_definition');
          try
            D.LoadFromJSON(JSON.ReadObject(I));
            D.Insert;
          except
            on Ex: Exception do
            begin
              if (not DoReset) then
              begin
                D.Free;
              end;
              E := EZYDefinitionJSONException.Create(Ex.Message);
              E.DefinitionNumber := I + 1;
              E.JSONString := TJSONFormatter.Format(JSON.ReadObject(I).InnerObject, true);
              raise E;
            end;
          end;
          if (Assigned(FOnWork)) then
          begin
            FOnWork(Self, I + 1);
          end;
        end;
      finally
        FDelayDefinitionRegistration := false;
        if (Assigned(FOnWorkEnd)) then
        begin
          FOnWorkEnd(Self);
        end;
      end;
    except
      if (DoReset) then Reset;
      raise;
    end;
  finally
    EndUpdate;
  end;
end;

procedure TZYInstructionEditor.RaiseDefinitionEvent(Id: Integer;
  Definition: TZYInstructionDefinition);
begin
  if Assigned(FDefinitionEvent[Id]) then
  begin
    FDefinitionEvent[Id](Self, Definition);
  end;
end;

procedure TZYInstructionEditor.RaiseNodeEvent(Id: Integer; Node: TZYInstructionTreeNode);
begin
  if Assigned(FNodeEvent[Id]) then
  begin
    FNodeEvent[Id](Self, Node);
  end;
end;

procedure TZYInstructionEditor.RegisterDefinition(Definition: TZYInstructionDefinition);
var
  I: Integer;
begin
  // This method is automatically called by TZYInstructionDefinition.Create

  if (FDelayDefinitionRegistration) and (not Definition.Inserted) then
  begin
    Exit;
  end;

  Assert(not FDefinitions.Contains(Definition));
  FDefinitions.BinarySearch(Definition, I, TComparer<TZYInstructionDefinition>.Construct(
    function(const Left, Right: TZYInstructionDefinition): Integer
    begin
      Result := Left.CompareTo(Right);
    end));
  FDefinitions.Insert(I, Definition);
end;

procedure TZYInstructionEditor.RemoveDefinition(Definition: TZYInstructionDefinition);
var
  N, P, T: TZYInstructionTreeNode;
  I: Integer;
  DoRemove: Boolean;
begin
  if (not Assigned(Definition.Parent)) then
  begin
    Exit;
  end;
  BeginUpdate;
  try
    N := Definition.Parent;
    N.RemoveDefinition(Definition);
    if (N.DefinitionCount > 0) then
    begin
      Exit;
    end;
    // Remove nodes without children
    DoRemove := true;
    while (DoRemove and Assigned(N) and (not (itnfIsRootNode in N.Flags))) do
    begin
      if (itnfIsLeafNode in N.Flags) then
      begin
        DoRemove := (N.DefinitionCount = 0);
      end else
      begin
        DoRemove := (not (itnfIsStaticNode in N.Flags)) and
          ((N.ChildCount = 0) or (TZYInstructionFilterInfo.Info[N.FilterClass].IsOptional and
          (N.ChildCount = 1) and (Assigned(N.Childs[0]))));
      end;
      if (DoRemove) then
      begin
        Assert(Assigned(N.Parent));
        P := N.Parent;
        I := P.IndexOf(N);
        if (not (itnfIsLeafNode in N.Flags)) and (Assigned(N.Childs[0])) then
        begin
          T := N.Childs[0];
          N.SetChildItem(0, nil);
          P.SetChildItem(I, T);
        end else
        begin
          P.SetChildItem(I, nil);
        end;
        N.Free;
        N := P;
      end;
    end;
  finally
    EndUpdate;
  end;
end;

procedure TZYInstructionEditor.Reset;
var
  I: Integer;
begin
  BeginUpdate;
  try
    FPreventDefinitionRemoval := true;
    for I := FDefinitions.Count - 1 downto 0 do
    begin
      FDefinitions[I].Free;
    end;
    FPreventDefinitionRemoval := false;
    FDefinitions.Clear;
    if (not Assigned(FRootNode)) then
    begin
      // 1, 2 and 3 Byte Opcode Tables
      FRootNode := TZYInstructionTreeNode.Create(Self, ifcOpcode, true, true);
      FRootNode.CreateChildNodeAtIndex($0F, ifcOpcode, false, true);
      FRootNode.Childs[$0F].CreateChildNodeAtIndex($38, ifcOpcode, false, true);
      FRootNode.Childs[$0F].CreateChildNodeAtIndex($3A, ifcOpcode, false, true);
      // 3DNow Table
      FRootNode.Childs[$0F].CreateChildNodeAtIndex($0F, ifcOpcode, false, true);
      // 3 Byte VEX Table
      FRootNode.CreateChildNodeAtIndex($C4, ifcVEX, false, true);
      // 2 Byte VEX Table (we copy the 3 byte VEX table later)
      FRootNode.CreateChildNodeAtIndex($C5, ifcVEX, false, true);
      // XOP Table
      FRootNode.CreateChildNodeAtIndex($8F, ifcXOP, false, true);
      // EVEX / MVEX Table
      FRootNode.CreateChildNodeAtIndex($62, ifcEMVEX, false, true);
    end;
  finally
    EndUpdate;
  end;
end;

procedure TZYInstructionEditor.SaveToFile(const Filename: String);
var
  JSON: TJSONArray;
  Writer: IJSONArrayWriter;
  List: TStringList;
begin
  JSON := TJSONArray.Create;
  try
    Writer := TJSONArrayWriter.Create(JSON);
    SaveToJSON(Writer);
    List := TStringList.Create;
    try
      List.Text := TJSONFormatter.Format(JSON, true);
      List.SaveToFile(FileName);
    finally
      List.Free;
    end;
  finally
    JSON.Free;
  end;
end;

procedure TZYInstructionEditor.SaveToJSON(const JSON: IJSONArrayWriter);
var
  I: Integer;
begin
  // Save to JSON
  if (Assigned(FOnWorkStart)) then
  begin
    FOnWorkStart(Self, 0, FDefinitions.Count);
  end;
  for I := 0 to FDefinitions.Count - 1 do
  begin
    FDefinitions[I].SaveToJSON(JSON.AddObject);
    if (Assigned(FOnWork)) then
    begin
      FOnWork(Self, I + 1);
    end;
  end;
  if (Assigned(FOnWorkEnd)) then
  begin
    FOnWorkEnd(Self);
  end;
end;

procedure TZYInstructionEditor.SetDefinitionEvent(Index: Integer;
  const Value: TZYEditorDefinitionEvent);
begin
  FDefinitionEvent[Index] := Value;
end;

procedure TZYInstructionEditor.SetNodeEvent(Index: Integer; const Value: TZYEditorNodeEvent);
begin
  FNodeEvent[Index] := Value;
end;

procedure TZYInstructionEditor.UnregisterDefinition(Definition: TZYInstructionDefinition);
begin
  // This method is automatically called by TZYInstructionDefinition.Destroy
  Assert(FDefinitions.Contains(Definition));
  // The list-class causes serious performance issues, if we remove multiple definitions in a
  // random order.
  // I disabled this operation for the @c Reset and @c Destroy methods and replaced it by a much
  // faster @c Clear call.
  if (not FPreventDefinitionRemoval) then
  begin
    FDefinitions.Remove(Definition);
  end;
end;
{$ENDREGION}

end.
