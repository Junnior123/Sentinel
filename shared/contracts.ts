import { z } from 'zod';
export const bounded = z.string().max(1024);
export const scopeSchema = z.object({ gameFiles:z.literal(true), allFiles:z.boolean().optional(), hardwareChanges:z.boolean().optional(), extraFolders:z.boolean(), executionTraces:z.boolean(), bannedModIds:z.array(z.string().regex(/^[a-z0-9_.-]{1,80}$/)).max(100) }).strict();
export type ScanScope=z.infer<typeof scopeSchema>;
export const fileSchema=z.object({id:z.string().uuid(),path:bounded,size:z.number().nonnegative().safe(),sha256:z.string().regex(/^[a-f0-9]{64}$/).nullable(),hashReason:bounded.nullable(),format:z.string().max(40),status:z.enum(['complete','partial','skipped','error','changed']),signature:z.string().max(80).nullable(),modIds:z.array(z.string().max(100)).max(100)}).strict();
export const artifactSchema=z.object({source:z.enum(['process','bam','prefetch','hardware','boot','recycle-bin','usn']),path:bounded,time:z.string().max(80).nullable(),fileId:z.string().uuid().nullable(),association:z.enum(['path-only','basename-only','none']),note:bounded}).strict();
export const findingSchema=z.object({ruleId:z.string().max(100),fileId:z.string().uuid().nullable(),category:z.enum(['known','review','policy']),title:z.string().max(200),evidence:z.array(bounded).max(20),source:z.string().url().max(1024)}).strict();
export const coverageSchema=z.object({collector:z.string().max(100),status:z.enum(['complete','partial','denied','unsupported','cancelled','failed']),reason:bounded,inspected:z.number().int().nonnegative()}).strict();
export const chunkSchema=z.object({files:z.array(fileSchema).max(300),artifacts:z.array(artifactSchema).max(300),findings:z.array(findingSchema).max(300),coverage:z.array(coverageSchema).max(100)}).strict();
export const summarySchema=z.object({schemaVersion:z.literal(1),ruleVersion:z.string().max(80),completion:z.enum(['complete','partial','cancelled','failed']),submission:z.enum(['complete','partial']),started:z.string().datetime({offset:true}),finished:z.string().datetime({offset:true}),files:z.number().int().nonnegative().max(250000),omitted:z.number().int().nonnegative().max(250000),known:z.number().int().nonnegative(),review:z.number().int().nonnegative(),policy:z.number().int().nonnegative()}).strict();
export type FileRecord=z.infer<typeof fileSchema>;
export type ExecutionArtifact=z.infer<typeof artifactSchema>;
export type Finding=z.infer<typeof findingSchema>;
export type Coverage=z.infer<typeof coverageSchema>;
export type ReportChunk=z.infer<typeof chunkSchema>;
export type ReportSummary=z.infer<typeof summarySchema>;
export interface ScanReport {summary:ReportSummary;chunks:ReportChunk[]}
export interface ScanSession {id:string;nickname:string;reason:string;scope:ScanScope;state:string;created:number;expires:number;progress?:{stage:string;count:number};summary?:ReportSummary;review?:{verdict:string;note:string}}
