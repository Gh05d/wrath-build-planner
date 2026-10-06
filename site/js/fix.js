// The message the player sends back to their LLM.
export function fixRequest(issues) {
  const lines = issues.filter(i => !i.note).map((i, n) => `${n + 1}. ${i.where ? `${i.where}: ` : ''}${i.message}`);
  return [
    'Fix these problems in the build and return the complete corrected JSON in one code block:',
    '',
    ...lines,
    '',
    'Keep everything else unchanged. Where a suggestion is the name the guide means, use it; leave out only what you cannot match.',
  ].join('\n');
}
