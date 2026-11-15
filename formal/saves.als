/*
 * Finance Overhaul — save / load safety (Alloy 6, temporal).
 *
 * One save file (Disk) and one running game. The player loads the city with or without the mod
 * (or with the mod but its Index flag off), plays days, buys Index shares, saves and quits, in any
 * order. We ask Alloy for traces that break a promise; "check" commands that find no
 * counterexample within the bounds hold for every trace up to that length.
 *
 * What the mod puts in a save (docs/features/roadmap.md, save compatibility):
 *   - Index shares: a VANILLA investment slot pointing at a mod-only prefab (dIndex).
 *   - Mod data blobs the game itself ignores: Orders, Bonds (with coupons paid), Mirror (proposed:
 *     a copy of the Index holding, so it can come back).
 *
 * Behaviour of the game without the mod (assumptions; Policy switches let us check both ways):
 *   - DropsUnknownPrefab: a slot whose prefab is missing is cleared on load (seen in game,
 *     save case 2; the city loads, the Index disappears).
 *   - keepsModData: saving without the mod writes the mod's blobs back unchanged (to verify).
 *
 * Abstractions: one bond paying one coupon per game day; "coupons due" = days played since the
 * bond was sold; one Index holding.
 */
module saves

enum Mode { WithMod, ModIndexOff, Vanilla }
enum Blob { Orders, Bonds, Mirror }

abstract sig Bool {}
one sig True, False extends Bool {}

/* Design and environment switches (free: every check below says which it assumes). */
one sig Policy {
  mirror: one Bool,        // mod mirrors the Index holding in mod data and restores it
  catchUp: one Bool,       // bond book pays every missed coupon on the first day back
  keepsModData: one Bool   // vanilla keeps mod blobs when it saves the city
}

one sig Disk {
  var dIndex: one Bool,     // the save's vanilla slot holds Index shares
  var dBlobs: set Blob,
  var dPaid: one Int,       // coupons the saved bond book has paid
  var dDay: one Int         // game day stored in the save (vanilla data)
}

one sig Game {
  var mode: lone Mode,      // none = main menu
  var index: one Bool,
  var blobs: set Blob,
  var paid: one Int,
  var day: one Int,
  var lastPaidStep: one Int // coupons paid by the most recent day step (for "lump" checks)
}

fun TrueIf[b: Bool]: set Bool { b & True }

/* ---------- initial state: a fresh city saved with the mod, a bond sold today, no shares ---------- */
fact init {
  Disk.dIndex = False
  Disk.dBlobs = Orders + Bonds
  Disk.dPaid = 0
  Disk.dDay = 0
  no Game.mode
  Game.index = False
  no Game.blobs
  Game.paid = 0
  Game.day = 0
  Game.lastPaidStep = 0
}

/* ---------- events ---------- */
pred unchangedDisk {
  Disk.dIndex' = Disk.dIndex
  Disk.dBlobs' = Disk.dBlobs
  Disk.dPaid' = Disk.dPaid
  Disk.dDay' = Disk.dDay
}

pred load[m: Mode] {
  no Game.mode
  Game.mode' = m
  Game.day' = Disk.dDay
  Game.paid' = Disk.dPaid
  Game.lastPaidStep' = 0
  m = WithMod implies {
    // the vanilla slot comes back as saved; with the mirror policy, a lost holding is restored
    Game.index' = ((Disk.dIndex = True or (Policy.mirror = True and Mirror in Disk.dBlobs))
      implies True else False)
    Game.blobs' = Disk.dBlobs
  }
  m = ModIndexOff implies {
    // mod running but the Index flag is off: the prefab is not registered, the slot is cleared
    Game.index' = False
    Game.blobs' = Disk.dBlobs
  }
  m = Vanilla implies {
    Game.index' = False                       // DropsUnknownPrefab
    Game.blobs' = Disk.dBlobs                 // still in memory; kept or dropped on save
  }
  unchangedDisk
}

pred playDay {
  some Game.mode
  Game.mode' = Game.mode
  Game.day' = add[Game.day, 1]
  Game.index' = Game.index
  Game.blobs' = Game.blobs
  (Game.mode in WithMod + ModIndexOff and Bonds in Game.blobs) implies {
    // coupons due = days since the sale; catch-up pays all of them, otherwise only today's
    let due = Game.day' |
      Policy.catchUp = True implies {
        Game.paid' = due
        Game.lastPaidStep' = sub[due, Game.paid]
      } else {
        Game.paid' = ((lt[Game.paid, due]) implies add[Game.paid, 1] else Game.paid)
        Game.lastPaidStep' = ((lt[Game.paid, due]) implies 1 else 0)
      }
  } else {
    Game.paid' = Game.paid
    Game.lastPaidStep' = 0
  }
  unchangedDisk
}

pred buyIndex {
  Game.mode = WithMod
  Game.mode' = Game.mode
  Game.index' = True
  Game.blobs' = ((Policy.mirror = True) implies Game.blobs + Mirror else Game.blobs)
  Game.paid' = Game.paid
  Game.day' = Game.day
  Game.lastPaidStep' = 0
  unchangedDisk
}

pred save {
  some Game.mode
  Disk.dIndex' = Game.index
  Disk.dDay' = Game.day
  Disk.dPaid' = Game.paid
  Game.mode = Vanilla implies {
    Disk.dBlobs' = ((Policy.keepsModData = True) implies Game.blobs else none)
  } else {
    // with the mod (any flag): the mirror follows the live holding
    Disk.dBlobs' = ((Policy.mirror = True and Game.index = True) implies Game.blobs + Mirror
      else ((Game.mode = WithMod) implies Game.blobs - Mirror else Game.blobs))
  }
  Game.mode' = Game.mode
  Game.index' = Game.index
  Game.blobs' = Game.blobs
  Game.paid' = Game.paid
  Game.day' = Game.day
  Game.lastPaidStep' = 0
}

pred quit {
  some Game.mode
  no Game.mode'
  Game.index' = False
  no Game.blobs'
  Game.paid' = Game.paid
  Game.day' = Game.day
  Game.lastPaidStep' = 0
  unchangedDisk
}

pred stutter {
  Game.mode' = Game.mode
  Game.index' = Game.index
  Game.blobs' = Game.blobs
  Game.paid' = Game.paid
  Game.day' = Game.day
  Game.lastPaidStep' = 0
  unchangedDisk
}

fact traces {
  always (
    (some m: Mode | load[m]) or playDay or buyIndex or save or quit or stutter
  )
}

/* ---------- the promises ---------- */

/* P1 (every situation): a city saved with the mod opens without it. With the mod's only vanilla
   footprint being the Index slot, a vanilla load never keeps a reference to the missing prefab. */
assert VanillaLoadIsClean {
  always (Game.mode = Vanilla implies Game.index = False)
}

/* P2 (retire the mod): once the player saves without the mod, the save holds nothing the game
   cannot read: no Index slot. Mod blobs may remain, inert. */
assert RetiredSaveIsVanilla {
  always ((Game.mode = Vanilla and save) implies after Disk.dIndex = False)
}

/* P3 (forgot to enable): Index shares that were SAVED with the mod are there again every later time
   the city is loaded with the mod (the player never sells in this model; shares bought and never
   saved were never in the save — Alloy's first counterexample, now excluded). */
assert ForgotToEnableKeepsIndex {
  always (
    (Game.mode = WithMod and Game.index = True and save) implies
      after always ((Game.mode = WithMod and before no Game.mode) implies Game.index = True)
  )
}

/* P4 (forgot to enable): the bond book is still there when the mod comes back. */
assert ForgotToEnableKeepsBonds {
  always ((Game.mode = WithMod) implies Bonds in Game.blobs)
}

/* P5 (any): the city never pays a coupon that is not due. */
assert NeverOverpays {
  always lte[Game.paid, Game.day]
}

/* P6 (forgot to enable, tester): coming back never pays more than one coupon in a single day. */
assert NoLumpOnReturn {
  always lte[Game.lastPaidStep, 1]
}

/* P7 (tester): switching the Index flag off and on does not lose shares. */
assert IndexFlagToggleKeepsShares {
  always (
    (Game.mode = WithMod and Game.index = True and save) implies
      after always ((Game.mode = WithMod and before no Game.mode) implies Game.index = True)
  )
}

/* ---------- checks (bounds: up to 10 steps, ints -16..15) ----------
   "expect 0": no counterexample (the promise holds); "expect 1": a counterexample or instance is
   expected (a known gap, or a reachability run). scripts/check-formal.sh runs them all. */

// Hold for any policy:
check VanillaLoadIsClean for 4 but 5 Int, 10 steps expect 0
check RetiredSaveIsVanilla for 4 but 5 Int, 10 steps expect 0
check NeverOverpays for 4 but 5 Int, 10 steps expect 0

// Forgot-to-enable today (no mirror): expect a counterexample (shares lost).
check ForgotToEnableKeepsIndex_Today {
  (Policy.mirror = False and Policy.keepsModData = True) implies
    (always ((Game.mode = WithMod and Game.index = True and save) implies
      after always ((Game.mode = WithMod and before no Game.mode) implies Game.index = True)))
} for 4 but 5 Int, 10 steps expect 1

// With the proposed mirror, and if vanilla keeps mod data: expect it to hold.
check ForgotToEnableKeepsIndex_Mirror {
  (Policy.mirror = True and Policy.keepsModData = True) implies
    (always ((Game.mode = WithMod and Game.index = True and save) implies
      after always ((Game.mode = WithMod and before no Game.mode) implies Game.index = True)))
} for 4 but 5 Int, 10 steps expect 0

// Even with the mirror, if vanilla drops mod data on save: expect a counterexample.
check ForgotToEnableKeepsIndex_DataDropped {
  (Policy.mirror = True and Policy.keepsModData = False) implies
    (always ((Game.mode = WithMod and Game.index = True and save) implies
      after always ((Game.mode = WithMod and before no Game.mode) implies Game.index = True)))
} for 4 but 5 Int, 10 steps expect 1

// Bonds come back only if vanilla keeps mod data.
check ForgotToEnableKeepsBonds_Kept {
  Policy.keepsModData = True implies (always (Game.mode = WithMod implies Bonds in Game.blobs))
} for 4 but 5 Int, 10 steps expect 0

check ForgotToEnableKeepsBonds_Dropped {
  Policy.keepsModData = False implies (always (Game.mode = WithMod implies Bonds in Game.blobs))
} for 4 but 5 Int, 10 steps expect 1

// Catch-up pays a lump after days without the mod; skipping never does.
check NoLumpOnReturn_CatchUp {
  Policy.catchUp = True implies always lte[Game.lastPaidStep, 1]
} for 4 but 5 Int, 10 steps expect 1

check NoLumpOnReturn_Skip {
  Policy.catchUp = False implies always lte[Game.lastPaidStep, 1]
} for 4 but 5 Int, 10 steps expect 0

// Tester: Index flag off and on, mirror on and data kept: expect it to hold.
check IndexFlagToggle_Mirror {
  (Policy.mirror = True and Policy.keepsModData = True) implies
    (always ((Game.mode = WithMod and Game.index = True and save) implies
      after always ((Game.mode = WithMod and before no Game.mode) implies Game.index = True)))
} for 4 but 5 Int, 10 steps expect 0

// Sanity: the situations are reachable (no vacuous passes).
run ForgotToEnableScenario {
  eventually (Game.mode = WithMod and Game.index = True and
    eventually (Game.mode = Vanilla and eventually (Game.mode = WithMod)))
} for 4 but 5 Int, 10 steps expect 1
