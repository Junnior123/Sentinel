export const reportSections = ['findings','artifacts','coverage'] as const;
export type ReportSection = typeof reportSections[number];
export function isReportSection(value:string):value is ReportSection {
 return reportSections.some(section=>section===value);
}
export async function readSection(db:D1Database,id:string,section:ReportSection,page:number){
 // The matching partial index avoids walking legacy inventory-only chunks.
 const rows=await db.prepare(`SELECT j.value FROM chunks c,json_each(c.data,'$.${section}') j WHERE c.scan_id=? AND json_array_length(c.data,'$.${section}')>0 ORDER BY c.seq,CAST(j.key AS INTEGER) LIMIT 101 OFFSET ?`).bind(id,page*100).all<{value:string}>();
 return {items:rows.results.slice(0,100).map(row=>JSON.parse(row.value)),hasMore:rows.results.length>100,page};
}
