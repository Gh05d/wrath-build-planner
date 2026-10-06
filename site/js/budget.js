// Point-buy check for the start block. Costs and budget from the game (IL 2026-10-06):
// StatsDistribution.s_StatValueCosts is indexed by the score; LevelUpState starts the distribution with 25 points
// unless the unit's blueprint has a StartingStatPointsComponent (mercenaries can differ).
import { normalize } from './match.js';

const COST = { 7: -4, 8: -2, 9: -1, 10: 0, 11: 1, 12: 2, 13: 3, 14: 5, 15: 7, 16: 10, 17: 13, 18: 17 };
const BUDGET = 25;
const ATTRIBUTES = ['Strength', 'Dexterity', 'Constitution', 'Intelligence', 'Wisdom', 'Charisma'];

function attribute(key) {
  const k = normalize(key);
  return ATTRIBUTES.find(a => normalize(a) === k || normalize(a.substring(0, 3)) === k);
}

export function checkPointBuy(build) {
  const scores = build?.start?.abilityScores;
  if (!scores || typeof scores !== 'object') return [];
  const values = Object.fromEntries(ATTRIBUTES.map(a => [a, 10]));   // the game starts every attribute at 10
  for (const [key, raw] of Object.entries(scores)) {
    const name = attribute(key);
    const value = typeof raw === 'string' ? parseInt(raw, 10) : raw;
    if (!name || !(value in COST)) return [];   // the format check reports unknown names and out-of-range values
    values[name] = value;
  }
  const cost = ATTRIBUTES.reduce((sum, a) => sum + COST[values[a]], 0);
  if (cost <= BUDGET) return [];
  return [{ error: false, where: 'start.abilityScores',
    message: `These scores cost ${cost} points; character creation gives ${BUDGET} (main character), so the mod cannot reach them all. Lower some scores, or check whether the guide gives final scores that include racial bonuses.` }];
}
