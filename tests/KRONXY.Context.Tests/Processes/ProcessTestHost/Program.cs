using System.Text;

switch (args.ElementAtOrDefault(0))
{
    case "echo-arguments":
        foreach (var argument in args.Skip(1))
        {
            Console.Out.WriteLine(Convert.ToBase64String(Encoding.UTF8.GetBytes(argument)));
        }
        return 0;
    case "streams":
        Console.Out.Write(args.ElementAtOrDefault(1));
        Console.Error.Write(args.ElementAtOrDefault(2));
        return int.Parse(args.ElementAtOrDefault(3) ?? "0", System.Globalization.CultureInfo.InvariantCulture);
    case "delay":
        await Task.Delay(int.Parse(args.ElementAtOrDefault(1) ?? "0", System.Globalization.CultureInfo.InvariantCulture));
        return 0;
    case "stdout":
        Console.Out.Write(new string('o', int.Parse(args.ElementAtOrDefault(1) ?? "0", System.Globalization.CultureInfo.InvariantCulture)));
        return 0;
    case "stderr":
        Console.Error.Write(new string('e', int.Parse(args.ElementAtOrDefault(1) ?? "0", System.Globalization.CultureInfo.InvariantCulture)));
        return 0;
    case "both":
        var size = int.Parse(args.ElementAtOrDefault(1) ?? "0", System.Globalization.CultureInfo.InvariantCulture);
        Console.Out.Write(new string('o', size));
        Console.Error.Write(new string('e', size));
        return 0;
    case "utf8":
        var utf8Bytes = Encoding.UTF8.GetBytes(new string(
            'é',
            int.Parse(args.ElementAtOrDefault(1) ?? "0", System.Globalization.CultureInfo.InvariantCulture)));
        await Console.OpenStandardOutput().WriteAsync(utf8Bytes);
        return 0;
    case "spawn-child":
        using (var child = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = Environment.ProcessPath!,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        })
        {
            child.StartInfo.ArgumentList.Add("delay");
            child.StartInfo.ArgumentList.Add(args.ElementAtOrDefault(1) ?? "0");
            child.Start();
            await File.WriteAllTextAsync(args.ElementAtOrDefault(2)!, child.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        return 0;
    default:
        return 64;
}
