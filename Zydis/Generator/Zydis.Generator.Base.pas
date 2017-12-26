{***************************************************************************************************

  Zydis Code Generator

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

unit Zydis.Generator.Base;

interface

uses
  System.Classes;

type
  TZYBaseGenerator = class;

  TZYGeneratorTask = class abstract(TObject)
  strict protected
    class procedure WorkStart(AGenerator: TZYBaseGenerator; ATotalWorkCount: Integer); static;
    class procedure WorkStep(AGenerator: TZYBaseGenerator); static;
    class procedure WorkEnd(AGenerator: TZYBaseGenerator); static;
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

  TZYGeneratorWorkStartEvent = procedure(Sender: TObject; AModuleId, ATaskId: Integer;
    TotalWorkCount: Integer) of Object;
  TZYGeneratorWorkEvent      = procedure(Sender: TObject; AWorkCount: Integer) of Object;
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
    function GetModule(AIndex: Integer): TZYGeneratorModuleInfo; inline;
    function GetModuleCount: Integer; inline;
  strict protected
    procedure InitGenerator(var AModuleInfo: TArray<TZYGeneratorModuleInfo>); virtual; abstract;
  strict protected
    procedure Reset; inline;
  strict protected
    constructor Create;
  protected
    procedure SkipTask; inline;
    procedure WorkStart(ATotalWorkCount: Integer); inline;
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

class procedure TZYGeneratorTask.WorkEnd(AGenerator: TZYBaseGenerator);
begin
  AGenerator.WorkEnd;
end;

class procedure TZYGeneratorTask.WorkStart(AGenerator: TZYBaseGenerator; ATotalWorkCount: Integer);
begin
  AGenerator.WorkStart(ATotalWorkCount);
end;

class procedure TZYGeneratorTask.WorkStep(AGenerator: TZYBaseGenerator);
begin
  AGenerator.WorkStep;
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

function TZYBaseGenerator.GetModule(AIndex: Integer): TZYGeneratorModuleInfo;
begin
  Result := FModuleInfo[AIndex];
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

procedure TZYBaseGenerator.WorkStart(ATotalWorkCount: Integer);
begin
  FCurrentWorkCount := 0;
  if Assigned(FOnWorkStart) then
  begin
    FOnWorkStart(Self, FCurrentModuleId, FCurrentTaskId, ATotalWorkCount);
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
