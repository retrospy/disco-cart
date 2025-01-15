

using System.IO.Ports;

SerialPort? port;
port = new("COM38", 4608000);
port.DtrEnable = true;
port.RtsEnable = true;
port.Open();

port.Write("ERA%");
Thread.Sleep(10000);

for (int i = 0; i < 16; ++i)
{
    Console.WriteLine("Set dial to " + (i + 1) + " and press any key...");
    Console.ReadLine();

    for (int j = 0; j < 40; ++j)
    {
        port.Write(string.Format("W{0:x}:{1:x}%", j * 0x10000, Convert.ToString(i + 1, 16)));
    }
}

char[] garbage = new char[100];
while(port.BytesToRead > 0)
    port.Read(garbage, 0, 100);

for (int i = 0; i < 16; ++i)
{
    Console.WriteLine("Set dial to " + (i + 1).ToString() + " and press any key...");
    Console.ReadLine();

    bool failed = false;

    for (int j = 0; j < 40; ++j)
    {
        port.Write(string.Format("R{0:x}%", j * 0x10000, i + 1));

        char[] buffer = new char[4];

        port.Read(buffer, 0, 1);
        port.Read(buffer, 1, 1);
        port.Read(buffer, 2, 1);
        port.Read(buffer, 3, 1);
        port.Read(garbage, 0, 1);
        port.Read(garbage, 0, 1);

        string val = new string(buffer);

        short intVal = short.Parse(val, System.Globalization.NumberStyles.HexNumber);

        if (intVal != i + 1)
        {
            failed = true;
            break;
        }
    }

    if (!failed)
        Console.WriteLine("PASSED");
    else
        Console.WriteLine("FAILED");
}

port.Close();

