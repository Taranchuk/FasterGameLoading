using RimWorld;
using System;
using Verse;

namespace FasterGameLoading
{
    public static class GraphicLoadingUtils
    {
        public static bool ShouldBeLoadedImmediately(this ThingDef thingDef)
        {
            return thingDef.IsBlueprint
                || thingDef.graphicData != null && thingDef.graphicData.Linked
                || thingDef.thingClass != null && thingDef.thingClass.Name == "Building_Pipe"
                || typeof(Medicine).IsAssignableFrom(thingDef.thingClass)
                || thingDef.orderedTakeGroup?.defName == "Medicine";
        }
    }
}
