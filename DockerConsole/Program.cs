using System;
using System.Threading;

namespace DockerConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello Docker console v1 :)");

            var a1 = Environment.GetEnvironmentVariable("Arg1");
            var a2 = Environment.GetEnvironmentVariable("Arg2");

            Console.WriteLine($"Arg1={a1}, Arg2={a2}");

            for (int i = 0; i < 100; i++)
            {
                Thread.Sleep(1000);

                Console.WriteLine(i);
            }
        }
    }
}
