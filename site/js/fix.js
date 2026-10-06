// The message the player sends back to their LLM.
export function fixRequest(issues) {
  const lines = issues.filter(i => !i.note).map((i, n) => `${n + 1}. ${i.where ? `${i.where}: ` : ''}${i.message}`);
  return [
    'Fix these problems in the build and return the complete corrected JSON in one code block:',
    '',
    ...lines,
    '',
    'Keep everything else unchanged. Leave out what you are unsure of instead of guessing.',
  ].join('\n');
}
