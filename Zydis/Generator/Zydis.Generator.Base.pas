unit Zydis.Generator.Base;

interface

uses
  System.Classes;

type
  TZYBaseGenerator = class;

  TZYGeneratorTask = class abstract(TObject)
  strict protected
    class procedure WorkStart(Generator: TZYBaseGenerator; TotalWorkCount: Integer); static;
    class procedure WorkStep(Generator: TZYBaseGenerator); static;
    class procedure WorkEnd(Generator: TZYBaseGenerator); static;
  strict protected
    constructor Create;
  end;

  TZYGeneratorTaskInfo = record
  public
    Description: String;
  end;

  TZYGeneratorModuleInfo = record
  public
    Description: String;
    Tasks: TArray<TZYGeneratorTaskInfo>;
  end;

  TZYGeneratorWorkStartEvent = procedure(Sender: TObject; ModuleId, TaskId: Integer;
    TotalWorkCount: Integer) of Object;
  TZYGeneratorWorkEvent      = procedure(Sender: TObject; WorkCount: Integer) of Object;
  TZYGeneratorWorkEndEvent   = TNotifyEvent;

  TZYBaseGenerator = class abstract(TObject)
  strict private
    FModuleInfo: TArray<TZYGeneratorModuleInfo>;
    FCurrentModuleId: Integer;
    FCurrentTaskId: Integer;
    FCurrentWorkCount: Integer;
  strict private
    FOnWorkStart: TZYGeneratorWorkStartEvent;
    FOnWork: TZYGeneratorWorkEvent;
    FOnWorkEnd: TZYGeneratorWorkEndEvent;
  strict private
    function GetModule(Index: Integer): TZYGeneratorModuleInfo; inline;
    function GetModuleCount: Integer; inline;
  strict protected
    procedure InitGenerator(var ModuleInfo: TArray<TZYGeneratorModuleInfo>); virtual; abstract;
  strict protected
    procedure Reset; inline;
  strict protected
    constructor Create;
  protected
    procedure SkipTask; inline;
    procedure WorkStart(TotalWorkCount: Integer); inline;
    procedure WorkStep; inline;
    procedure WorkEnd; inline;
  public
    property Module[Index: Integer]: TZYGeneratorModuleInfo read GetModule;
    property ModuleCount: Integer read GetModuleCount;
  public
    property OnWorkStart: TZYGeneratorWorkStartEvent read FOnWorkStart write FOnWorkStart;
    property OnWork: TZYGeneratorWorkEvent read FOnWork write FOnWork;
    property OnWorkEnd: TZYGeneratorWorkEndEvent read FOnWorkEnd write FOnWorkEnd;
  end;

implementation

uses
  System.SysUtils;

{$REGION 'Class: TZYGeneratorTask'}
constructor TZYGeneratorTask.Create;
begin
  inherited Create;
end;

class procedure TZYGeneratorTask.WorkEnd(Generator: TZYBaseGenerator);
begin
  Generator.WorkEnd;
end;

class procedure TZYGeneratorTask.WorkStart(Generator: TZYBaseGenerator; TotalWorkCount: Integer);
begin
  Generator.WorkStart(TotalWorkCount);
end;

class procedure TZYGeneratorTask.WorkStep(Generator: TZYBaseGenerator);
begin
  Generator.WorkStep;
end;
{$ENDREGION}

{$REGION 'Class: TZYBaseGenerator'}
constructor TZYBaseGenerator.Create;
begin
  inherited Create;
  InitGenerator(FModuleInfo);
  if (Length(FModuleInfo) = 0) then
  begin
    raise Exception.Create('No module info provided.');
  end;
end;

function TZYBaseGenerator.GetModule(Index: Integer): TZYGeneratorModuleInfo;
begin
  Result := FModuleInfo[Index];
end;

function TZYBaseGenerator.GetModuleCount: Integer;
begin
  Result := Length(FModuleInfo);
end;

procedure TZYBaseGenerator.Reset;
begin
  FCurrentModuleId := 0;
  FCurrentTaskId := 0;
  FCurrentWorkCount := 0;
end;

procedure TZYBaseGenerator.SkipTask;
begin
  Inc(FCurrentTaskId);
  if (FCurrentTaskId > High(FModuleInfo[FCurrentModuleId].Tasks)) then
  begin
    Inc(FCurrentModuleId);
    if (FCurrentModuleId > High(FModuleInfo)) then
    begin
      raise EListError.CreateFmt('Index out of bounds (%d)', [FCurrentModuleId]);
    end;
    FCurrentTaskId := 0;
    if (Length(FModuleInfo[FCurrentModuleId].Tasks) = 0) then
    begin
      FCurrentTaskId := -1;
    end;
  end;
end;

procedure TZYBaseGenerator.WorkEnd;
begin
  if Assigned(FOnWorkEnd) then
  begin
    FOnWorkEnd(Self);
  end;
  if (FCurrentModuleId < High(FModuleInfo)) or
    (FCurrentTaskId < High(FModuleInfo[FCurrentModuleId].Tasks)) then
  begin
    SkipTask;
  end;
end;

procedure TZYBaseGenerator.WorkStart(TotalWorkCount: Integer);
begin
  FCurrentWorkCount := 0;
  if Assigned(FOnWorkStart) then
  begin
    FOnWorkStart(Self, FCurrentModuleId, FCurrentTaskId, TotalWorkCount);
  end;
end;

procedure TZYBaseGenerator.WorkStep;
begin
  Inc(FCurrentWorkCount);
  if Assigned(FOnWork) then
  begin
    FOnWork(Self, FCurrentWorkCount);
  end;
end;
{$ENDREGION}

end.
