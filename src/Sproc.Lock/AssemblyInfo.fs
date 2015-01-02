namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("Sproc.Lock")>]
[<assembly: AssemblyProductAttribute("Sproc.Lock")>]
[<assembly: AssemblyDescriptionAttribute("SQL Server based distributed locking.")>]
[<assembly: AssemblyVersionAttribute("0.5.1")>]
[<assembly: AssemblyFileVersionAttribute("0.5.1")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "0.5.1"
