using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiveSplit.Model;
public class PausedRun
{
    public Attempt? InProgressAttempt { get; set; }
    public IList<Time> InProgressTimes { get; set; }

    public bool Exists => InProgressAttempt != null;

    public PausedRun()
    {
        InProgressTimes = [];
    }
}
