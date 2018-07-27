using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Microsoft.Research.Naiad;
using Microsoft.Research.Naiad.Dataflow;
using Microsoft.Research.Naiad.Input;
using Microsoft.Research.Naiad.Frameworks.Lindi;
using Microsoft.Research.Naiad.Frameworks.Hdfs;
using Microsoft.Research.Naiad.Dataflow.PartitionBy;

namespace NaiadProba
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			int numDays = int.Parse (args [1]);

			using (var computation = NewComputation.FromArgs(ref args))
			{

				//Console.WriteLine ("computation.Configuration.ProcessID: " + computation.Configuration.ProcessID);

				var initYesterdayCounts = Enumerable.Empty<Pair<int, int>>().AsNaiadStream (computation);
				Stream<int, Epoch> dayInit = null;
				if (computation.Configuration.ProcessID == 0) {
					dayInit = new[] { 1 }.AsNaiadStream (computation);
				} else {
					dayInit = Enumerable.Empty<int>().AsNaiadStream (computation);
				}

				initYesterdayCounts.Iterate ((lc, yesterdayCounts) => {
					
					var dayDelayed = lc.Delay<int>(numDays - 1);
					var dayIngress = lc.EnterLoop(dayInit);
					var dayHead = dayIngress.Concat(dayDelayed.Output);

					var dayTail = dayHead.Select(x => x + 1);

					dayDelayed.Input = dayTail;








					//var visits = day.SelectMany(x => ("/home/ggevay/Dropbox/cfl_testdata/ClickCount/in/clickLog_" + x).ReadLinesOfText());
					var visits = dayHead.PartitionBy(x => x).SelectMany(x => (args[0] + x).ReadLinesOfText());
					//var uri = day.Select(x => new Uri("hdfs://cloud-11:44000/user/ggevay/ClickCountGenerated/0.05/25000000/in/clickLog_" + x));
					//var visits = uri.FromHdfsText();



					visits = visits.PartitionBy(x => x.GetHashCode());

					var todayCounts = visits
						.Select (x => int.Parse (x).PairWith (1))
						.Aggregate (p => p.First, p => p.Second, (x, y) => x + y, (key, state) => key.PairWith(state));

					var summed = todayCounts
						.Join (yesterdayCounts, x => x.First, x => x.First, (x, y) => Math.Abs(x.Second - y.Second))
						.Aggregate<int, int, int, int, IterationIn<Epoch>>(x => 0, x => x, (x, y) => x + y, (key, state) => state, true);

					lc.ExitLoop(summed).Subscribe(x =>
						{
							foreach (var line in x)
								Console.WriteLine(line);
						});

					return todayCounts;
				},
					numDays - 1,
					"ClickCount iteration");

				computation.Activate();
				computation.Join();
			}
		}
	}


	public static class ExtensionMethods
	{
		public static IEnumerable<string> ReadLinesOfText(this string filename)
		{
			Console.WriteLine("Reading file {0}", filename);

			if (System.IO.File.Exists(filename))
			{
				//var file = File.OpenText(filename);
				var file = new StreamReader(filename, Encoding.UTF8, true, 8*1024*1024);
				while (!file.EndOfStream)
					yield return file.ReadLine();
			}
			else
				Console.WriteLine("File not found! {0}", filename);
		}
	}
}
