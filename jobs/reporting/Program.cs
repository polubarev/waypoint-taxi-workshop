using System.Globalization;
using System.Text.Json;
if(args.Length!=1) { Console.Error.WriteLine("Usage: Reporting <event-json-file>"); return 2; }
using var doc=JsonDocument.Parse(await File.ReadAllTextAsync(args[0]));
var payload=doc.RootElement.GetProperty("data");
var pickup=payload.GetProperty("pickup");
var latitude=pickup.GetProperty("lat").GetDouble();
var longitude=pickup.GetProperty("lng").GetDouble();
Console.WriteLine(string.Create(CultureInfo.InvariantCulture,$"{payload.GetProperty("bookingId").GetGuid()},{latitude},{longitude}"));
return 0;
