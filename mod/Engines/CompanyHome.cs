using FinanceOverhaul.Core; // BuildingFraming, LocalCompany
using UnityEngine; // Vector3

// Game classes used without a using (global namespace, Assembly-CSharp): Building, BuildingManager,
//   CameraController, DefaultTool, InstanceID, ToolsModifierControl

namespace FinanceOverhaul
{
    /// <summary>
    /// Shows a local company's headquarters: the camera moves to the building, near enough to see it
    /// whole, and the building's own info panel opens, as a click on it would. Shared by the IPO
    /// dialog (View headquarters, with fireworks) and the Invest dialog (View headquarters).
    /// </summary>
    internal static class CompanyHome
    {
        /// <summary>The company's headquarters is the building it was (not lost or replaced).</summary>
        public static bool Stands(LocalCompany company)
        {
            if (company == null || company.Home.Id == 0)
            {
                return false;
            }

            Building home = BuildingManager.instance.m_buildings.m_buffer[company.Home.Id];
            return home.m_buildIndex == company.Home.BuildIndex
                && (home.m_flags & Building.Flags.Created) != 0;
        }

        /// <summary>False when the company has no standing headquarters (lost, rebuilding).</summary>
        public static bool Show(LocalCompany company, bool fireworks)
        {
            CameraController camera = ToolsModifierControl.cameraController;
            if (company == null || camera == null)
            {
                return false;
            }

            Building home = BuildingManager.instance.m_buildings.m_buffer[company.Home.Id];
            if (home.m_buildIndex != company.Home.BuildIndex)
            {
                return false;
            }

            var id = new InstanceID { Building = company.Home.Id };
            camera.SetTarget(id, home.m_position, true);
            // Near enough to see the building whole, whatever its size.
            Vector3 size = home.Info == null ? Vector3.zero : home.Info.m_size;
            camera.m_targetSize = BuildingFraming.Distance(
                home.Width * 8f,
                home.Length * 8f,
                size.y
            );
            camera.m_targetAngle.y = BuildingFraming.Pitch(size.y);
            if (fireworks)
            {
                IpoFireworks.Over(company.Home.Id, company.Home.BuildIndex);
            }

            // And the building's own info panel, as a click on it would open.
            DefaultTool.OpenWorldInfoPanel(id, home.m_position);
            ModLog.Info("company home: showing headquarters building " + company.Home.Id);
            return true;
        }
    }
}
