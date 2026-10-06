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
  const given = new Set();
  for (const [key, raw] of Object.entries(scores)) {
    const name = attribute(key);
    const value = typeof raw === 'string' ? parseInt(raw, 10) : raw;
    // Unknown names and out-of-range values are the format check's; two keys for one attribute are ambiguous.
    if (!name || !(value in COST) || given.has(name)) return [];
    given.add(name);
    values[name] = value;
  }
  const cost = ATTRIBUTES.reduce((sum, a) => sum + COST[values[a]], 0);
  const complete = given.size === ATTRIBUTES.length;   // with scores missing, the player spends the rest by hand
  const lower = 'Lower some scores, or check whether the guide gives final scores that include racial bonuses.';

  // "for" is a free-text hint: only a missing value or "main" pins the main character's 25 points.
  if (!build.for || normalize(build.for) === 'main') {
    if (cost > BUDGET)
      return [warning(`These scores cost ${cost} points; character creation gives ${BUDGET} (main character), so the mod cannot reach them all. ${lower}`)];
    // The game keeps Next disabled while points are unspent ("There are unspent points …").
    if (cost < BUDGET && complete)
      return [warning(`These scores cost ${cost} points; character creation gives ${BUDGET} (main character), and all points must be spent before the game lets you continue. Raise some scores so they add up to ${BUDGET}.`)];
    return [];
  }
  if (cost === BUDGET || cost === MERCENARY_BUDGET) return [];
  if (cost > BUDGET || complete)
    return [warning(`These scores cost ${cost} points; the main character gets ${BUDGET}, a mercenary ${MERCENARY_BUDGET}, and all points must be spent before the game lets you continue. Adjust the scores to one of the two.`)];
  return [];
}

const warning = message => ({ error: false, where: 'start.abilityScores', message });
