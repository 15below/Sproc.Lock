module Sproc.Lock.Tests

open System
open Sproc.Lock.Fun
open NUnit.Framework

let connString =
    match Environment.GetEnvironmentVariable "LockConnString" with
    | s when String.IsNullOrEmpty s ->
        "Server=(local);Database=Lock;Trusted_Connection=True;"
    | s -> s

[<Test>]
let ``Get global lock`` () =
    let lockId = Guid.NewGuid() |> sprintf "%A"
    use lock1 = GetGlobalLock connString (TimeSpan.FromSeconds 10.) lockId
    Assert.IsTrue (match lock1 with Locked _ -> true | _ -> false)

[<Test>]
let ``Global lock actually locks`` () =
    let lockId = Guid.NewGuid() |> sprintf "%A"
    use lock1 = GetGlobalLock connString (TimeSpan.FromSeconds 10.) lockId
    use lock2 = GetGlobalLock connString (TimeSpan.FromSeconds 10.) lockId
    Assert.AreEqual (Unavailable, lock2)

[<Test>]
let ``Get organisation lock`` () =
    let lockId = Guid.NewGuid() |> sprintf "%A"
    let org = "MyOrg"
    use lock1 = GetOrganisationLock connString org (TimeSpan.FromSeconds 10.) lockId
    Assert.IsTrue (match lock1 with Locked _ -> true | _ -> false)

[<Test>]
let ``Organisation lock actually locks`` () =
    let lockId = Guid.NewGuid() |> sprintf "%A"
    let org = "MyOrg"
    use lock1 = GetOrganisationLock connString org (TimeSpan.FromSeconds 10.) lockId
    use lock2 = GetOrganisationLock connString org (TimeSpan.FromSeconds 10.) lockId
    Assert.AreEqual (Unavailable, lock2)

[<Test>]
let ``Get environment lock`` () =
    let lockId = Guid.NewGuid() |> sprintf "%A"
    let org = "MyOrg"
    let env = "Test"
    use lock1 = GetEnvironmentLock connString org env (TimeSpan.FromSeconds 10.) lockId
    Assert.IsTrue (match lock1 with Locked _ -> true | _ -> false)

[<Test>]
let ``Environment lock actually locks`` () =
    let lockId = Guid.NewGuid() |> sprintf "%A"
    let org = "MyOrg"
    let env = "Test"
    use lock1 = GetEnvironmentLock connString org env (TimeSpan.FromSeconds 10.) lockId
    use lock2 = GetEnvironmentLock connString org env (TimeSpan.FromSeconds 10.) lockId
    Assert.AreEqual (Unavailable, lock2)

[<Test>]
let ``Negative maxDuration should fail`` () =
    let lock1 = GetGlobalLock connString (TimeSpan.FromSeconds -10.) "bob"
    Assert.AreEqual (Error -999, lock1)

