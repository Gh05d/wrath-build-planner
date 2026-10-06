// Point-buy check for the start block. Costs and budget from the game (IL 2026-10-06):
// StatsDistribution.s_StatValueCosts is indexed by the score; LevelUpState starts the distribution with 25 points
// unless the unit's blueprint has a StartingStatPointsComponent (mercenaries can differ).
import { normalize } from './match.js';

const COST = { 7: -4, 8: -2, 9: -1, 10: 0, 11: 1, 12: 2, 13: 3, 14: 5, 15: 7, 16: 10, 17: 13, 18: 17 };
const BUDGET = 25;
const MERCENARY_BUDGET = 20;   // shown in the mercenary creation window (Deck, 2026-10-06)
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
  // "for" other than the main character: the start block can only be a mercenary's, and a mercenary gets less.
  const forMain = !build.for || normalize(build.for) === 'main';
  const budget = forMain ? BUDGET : MERCENARY_BUDGET;
  const who = forMain ? `character creation gives ${BUDGET} (main character)` : `a mercenary gets ${MERCENARY_BUDGET}`;
  if (cost > budget)
    return [warning(`These scores cost ${cost} points; ${who}, so the mod cannot reach them all. Lower some scores, or check whether the guide gives final scores that include racial bonuses.`)];
  // The game keeps Next disabled while points are unspent ("There are unspent points …").
  if (cost < budget)
    return [warning(`These scores cost ${cost} points; ${who}, and all points must be spent before the game lets you continue. Raise some scores so they add up to ${budget}.`)];
  return [];
}

const warning = message => ({ error: false, where: 'start.abilityScores', message });
