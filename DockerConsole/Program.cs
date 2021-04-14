using System;
using System.Threading;

namespace DockerConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello Docke console!");

            for (int i = 0; i < 100; i++)
            {
                Thread.Sleep(1000);

                Console.WriteLine(i);
            }
        }
    }
}
