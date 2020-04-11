using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BackgroundWorkerSimple
{
    class MyWork
    {
        public static BackgroundWorker Worker { get; internal set; }

        internal static void Run(DoWorkEventArgs e)
        {
            for (int i = 1; i <= 100; i++)
            {
                if (Worker.CancellationPending == true)
                {
                    e.Cancel = true;
                    break;
                }
                else
                {
                    // Perform a time consuming operation and report progress.
                    System.Threading.Thread.Sleep(50);
                    Worker.ReportProgress(i);
                }
            }
        }
    }
}
