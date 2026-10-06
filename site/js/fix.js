// The message the player sends back to their LLM.
export function fixRequest(issues) {
  const lines = issues.filter(i => !i.note).map((i, n) => `${n + 1}. ${i.where ? `${i.where}: ` : ''}${i.message}`);
  // Guides usually give final scores; the format wants them before the racial bonus.
  if (issues.some(i => Number(/is (-?\d+); starting scores go from 7 to 18/.exec(i.message)?.[1]) > 18))
    lines.push(`${lines.length + 1}. If the guide gives final ability scores, remove the racial modifier (most races add +2 to one or two attributes) instead of lowering the score to 18.`);
  return [
    'Fix these problems in the build and return the complete corrected JSON in one code block:',
    '',
    ...lines,
    '',
    'Keep everything else unchanged. Where a suggestion is the name the guide means, use it; leave out only what you cannot match.',
  ].join('\n');
}
