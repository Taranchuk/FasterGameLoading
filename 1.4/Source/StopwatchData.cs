using HarmonyLib;
using System.Diagnostics;
using System.Reflection;
using Verse;

namespace FasterGameLoading
{
    public class StopwatchData
    {
        public float totalTime;
        public float count;
        public MethodBase targetMethod;
        public Stopwatch stopwatch;
        public StopwatchData(MethodBase targetMethod)
        {
            this.targetMethod = targetMethod;
            this.stopwatch = new Stopwatch();
        }
        public void Start()
        {
            stopwatch.Restart();
        }

        public void Stop()
        {
            stopwatch.Stop();
            var elapsed = (float)stopwatch.ElapsedTicks / Stopwatch.Frequency;
            count++;
            totalTime += elapsed;
        }

        public void LogTime()
        {
            //if (count < 50000 && totalTime >= 0.01f)
            {
                Log.Message(targetMethod.DeclaringType.FullName + "." + targetMethod.Name + " took " + totalTime + ", run count: " + count);
            }
        }
    }
}

