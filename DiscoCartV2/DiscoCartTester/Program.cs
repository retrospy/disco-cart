

using System.Collections.Generic;
using System.IO.Ports;

SerialPort? port;
port = new("COM38", 4608000);
port.DtrEnable = true;
port.RtsEnable = true;
port.Open();

port.Write("ERA%");
Thread.Sleep(10000);

byte[] buffer = new byte[512];

for (int i = 0; i < 16; ++i)
{
    Console.WriteLine("Set dial to " + (i + 1) + " and hit Enter...");
    Console.ReadLine();

    port.Write(string.Format("X{0:x}%", 0x3FFFFF));

    for (int j = 0; j < 400000; ++j)
    {
        port.Write(buffer, 0, 512);
    }
}

for (int i = 0; i < 1; ++i)
{
    bool failed = false;
    Console.WriteLine("Set dial to " + (i + 1).ToString() + " and press any key...");
    Console.ReadLine();

    port.Write(string.Format("S{0:x}%", 0x3FFFFF));

    int index = 0;
    do
    {
        int cnt = port.Read(buffer, 0, 512);
        index += cnt;

        for(int j = 0; i < cnt; ++j)
        {
            if (buffer[i] != 0)
            {
                failed = true;
                break;
            }
        }

    } while (!failed && index < ((0x3FFFFF + 1) * 2));

    if (failed)
        Console.WriteLine("FAILED");
    else
        Console.WriteLine("PASSED");
}


//    port.Write(string.Format("W{0:x}:{1:x}%", 0, Convert.ToString(i + 1, 16)));
//}

//char[] garbage = new char[100];
//while(port.BytesToRead > 0)
//    port.Read(garbage, 0, 100);

//for (int i = 0; i < 16; ++i)
//{
//    Console.WriteLine("Set dial to " + (i + 1).ToString() + " and press any key...");
//    Console.ReadLine();

//    port.Write(string.Format("R{0:x}%", 0, i + 1));

//    char[] buffer = new char[4];


//    port.Read(buffer, 0, 1);
//    port.Read(buffer, 1, 1);
//    port.Read(buffer, 2, 1);
//    port.Read(buffer, 3, 1);
//    port.Read(garbage, 0, 1);
//    port.Read(garbage, 0, 1);

//    string val = new string(buffer);

//    Int16 intVal = Int16.Parse(val, System.Globalization.NumberStyles.HexNumber);

//    if (intVal == i + 1)
//        Console.WriteLine("PASSED");
//    else
//        Console.WriteLine("FAILED: " + intVal + " != " + (i+1).ToString());
//}

port.Close();

