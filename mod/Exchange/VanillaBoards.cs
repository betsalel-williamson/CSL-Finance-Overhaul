using System;
using System.Collections.Generic;

namespace FinanceOverhaul
{
    /// <summary>
    /// Live ticker step 3: hides the Stock Exchange's baked scrolling boards (the
    /// three AnimUV sub-meshes) so only the live boards show. Uses the vanilla
    /// rendering gate: a sub-mesh is drawn only when the building has every flag in
    /// <c>MeshInfo.m_flagsRequired</c> (honoured by BuildingAI.RenderMeshes and the
    /// far-LOD group builders). Requiring <c>Deleted</c>, which a live building never
    /// has, hides it. Runtime only — nothing is saved; originals are restored on
    /// toggle-off, unload, disable and hot reload. A <c>Deleted</c> bit already present
    /// is ours (e.g. left by an older assembly), so the original is the value without it.
    /// </summary>
    public static class VanillaBoards
    {
        internal const Building.Flags HideFlag = Building.Flags.Deleted;
        private const string BoardShader = "AnimUV";

        private static readonly Dictionary<BuildingInfo.MeshInfo, Building.Flags> Originals =
            new Dictionary<BuildingInfo.MeshInfo, Building.Flags>();

        private static bool logged;

        public static void Hide(BuildingInfo info)
        {
            if (info == null || info.m_subMeshes == null)
            {
                return;
            }

            int hidden = 0;
            for (int i = 0; i < info.m_subMeshes.Length; i++)
            {
                BuildingInfo.MeshInfo mesh = info.m_subMeshes[i];
                if (mesh == null || !IsBoard(mesh))
                {
                    continue;
                }

                if (!Originals.ContainsKey(mesh))
                {
                    Originals[mesh] = mesh.m_flagsRequired & ~HideFlag;
                }

                if ((mesh.m_flagsRequired & HideFlag) == 0)
                {
                    mesh.m_flagsRequired |= HideFlag;
                    hidden++;
                }
            }

            if (hidden > 0 && !logged)
            {
                logged = true;
                ModLog.Info(
                    "ticker: hid " + hidden + " vanilla board sub-mesh(es) on '" + info.name + "'"
                );
            }
        }

        public static void RestoreAll()
        {
            if (Originals.Count == 0)
            {
                return;
            }

            foreach (KeyValuePair<BuildingInfo.MeshInfo, Building.Flags> kv in Originals)
            {
                try
                {
                    kv.Key.m_flagsRequired = kv.Value;
                }
                catch
                {
                    // fail soft
                }
            }

            ModLog.Info("ticker: restored " + Originals.Count + " vanilla board sub-mesh(es)");
            Originals.Clear();
            logged = false;
        }

        /// <summary>Whether the vanilla boards are hidden right now (game UI tests read it).</summary>
        internal static bool AnyHidden
        {
            get
            {
                foreach (BuildingInfo.MeshInfo mesh in Originals.Keys)
                {
                    if (mesh != null && (mesh.m_flagsRequired & HideFlag) != 0)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        internal static bool IsBoard(BuildingInfo.MeshInfo mesh)
        {
            BuildingInfoBase sub = mesh.m_subInfo;
            return sub != null
                && sub.m_material != null
                && sub.m_material.shader != null
                && sub.m_material.shader.name.IndexOf(BoardShader, StringComparison.Ordinal) >= 0;
        }
    }
}
