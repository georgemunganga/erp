export function evaluateCriteria(criteria: string, sample: { name: string; status: string; amount: number }): { matched: boolean; checks: string[] } {
  if (!criteria.trim()) return { matched: true, checks: ["No criteria: all sample records match."] };
  const clauses = criteria.split(/\s+AND\s+|\s*;\s*/i).filter(Boolean);
  const checks: string[] = [];
  const results = clauses.map((clause) => {
    const amount = clause.match(/^\s*amount\s*(>=|<=|>|<|=|==|!=)\s*(-?\d+(?:\.\d+)?)\s*$/i);
    if (amount) { const target = Number(amount[2]); const value = sample.amount; const matched = amount[1] === ">=" ? value >= target : amount[1] === "<=" ? value <= target : amount[1] === ">" ? value > target : amount[1] === "<" ? value < target : amount[1] === "!=" ? value !== target : value === target; checks.push(`${clause.trim()}: ${matched ? "matched" : "not matched"}`); return matched; }
    const string = clause.match(/^\s*(name|status)\s*(contains|is|=|==|!=)\s*["']?(.+?)["']?\s*$/i);
    if (string) { const value = string[1].toLowerCase() === "name" ? sample.name : sample.status; const target = string[3].replace(/^["']|["']$/g, "").trim(); const same = value.toLowerCase() === target.toLowerCase(); const matched = string[2].toLowerCase() === "contains" ? value.toLowerCase().includes(target.toLowerCase()) : string[2] === "!=" ? !same : same; checks.push(`${clause.trim()}: ${matched ? "matched" : "not matched"}`); return matched; }
    throw new Error(`Unsupported criterion: ${clause}. Use status=Overdue, amount>=500, or name contains workshop.`);
  });
  return { matched: results.every(Boolean), checks };
}
