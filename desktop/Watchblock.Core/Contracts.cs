using System.Text.Json;
namespace Watchblock.Core;
public record ScanScope(bool GameFiles=true,bool ExtraFolders=false,bool ExecutionTraces=true,string[]? BannedModIds=null,bool AllFiles=false,bool HardwareChanges=false);
public record FileRecord(string Id,string Path,long Size,string? Sha256,string? HashReason,string Format,string Status,string? Signature,string[] ModIds);
public record ExecutionArtifact(string Source,string Path,string? Time,string? FileId,string Association,string Note);
public record Finding(string RuleId,string? FileId,string Category,string Title,string[] Evidence,string Source);
public record Coverage(string Collector,string Status,string Reason,int Inspected);
public record ReportChunk(List<FileRecord> Files,List<ExecutionArtifact> Artifacts,List<Finding> Findings,List<Coverage> Coverage);
public record ReportSummary(int SchemaVersion,string RuleVersion,string Completion,string Submission,string Started,string Finished,int Files,int Omitted,int Known,int Review,int Policy);
public record ScanReport(ReportSummary Summary,List<ReportChunk> Chunks);
public record ScanSession(string Id,string Nickname,string Reason,ScanScope Scope,string State,long Created,long Expires,int ChunkCount=0);
public record ClaimResponse(ScanSession Session,string Token,string Operator,string Service,string Origin);
public record ScanProgress(string Stage,int Count,int? Total=null,double? Percent=null,double? RemainingSeconds=null);
public record Rule(string Id,string Title,string Category,string Source,string[] Sha256,string[] AllEntries,string[] AllStrings,string[] ExcludeSha256,string Validation,string[]? Formats=null,Dictionary<string,string>? EntrySha256=null,string[]? DeletedNameHints=null);
public record RulePack(int SchemaVersion,string Version,Rule[] Rules);
public static class Json {public static readonly JsonSerializerOptions Options=new(JsonSerializerDefaults.Web){WriteIndented=false,MaxDepth=40};}
