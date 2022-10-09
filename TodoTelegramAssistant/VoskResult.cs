using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TodoTelegramAssistant
{
    public class VoskResult
    {
        public IEnumerable<Result>? Result { get; set; }
        public string? Text { get; set; }
    }
    public class Result
    {
        public double Conf { get; set; }
        public double End { get; set; }
        public double Start { get; set; }
        public string? Word { get; set; }
    }
}
