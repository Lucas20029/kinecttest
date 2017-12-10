using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApplication3
{
    public class AppInfoHelper
    {
        static int currentSecond { get; set; }
        static int callsPerSecond = 0;
        static int lastResult = 0;
        public static int LoopCallToGetCallsPerSecond()
        {
            if (currentSecond == DateTime.Now.Second)
            {
                callsPerSecond++;
            }
            else
            {
                lastResult = callsPerSecond;
                callsPerSecond = 0;
                currentSecond = DateTime.Now.Second;
            }
            return lastResult;
        }
    }
}
