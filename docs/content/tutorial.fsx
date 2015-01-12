(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.
#I "../../bin"
module NastyAPI =
    let DoOp config = ()
    let DoAccountOp str = ()

(**
Distributed Locking via SQL Server
==================================

Sproc.Lock gives a quick and easy way to take and release locks across multiple servers,
using SQL Server as a back end.

Building Sproc.Lock will attempt to deploy it's schema as one of the build steps - by
default it will try to create/connect to a database called ``Lock`` on ``(local)`` using
the credentials of the user running the build scripts; setting an environment variable
called ``LockConnString`` with an alternative connection string will override this and
both deploy and test Sproc.Lock on the server of your choice.

Use a server is deployed, using the library is easy. Let's assume that we have a third
party service called ``NastyAPI`` which we only have one account for.

We'd like to write a service that only calls NastyAPI if no one else is, to avoid punitive
charges. (They're nasty, okay?)

*)
#r "Sproc.Lock.dll"
open System
open Sproc.Lock.Fun

let lserver = "SQL Server connection string here"

let GetNastyData () =
    use lock =
        GetGlobalLock lserver (TimeSpan.FromMinutes 5.) "NastyAPI"
    match lock with
    | Locked l ->
        NastyAPI.DoOp()
    | Unavailable ->
        () // Do nothing
    | Error i ->
        () // Sproc.Lock internal error occurred

(**

    [lang=csharp]
    /// <summary>
    /// Wrapper class; all examples below are static members
    /// within it.
    ///
    /// These examples use the Sproc.Lock.OO namespace
    /// </summary>
    public class DoWork
    {
        static LockProvider provider = new LockProvider("sql connection string");
        static public void GetNastyData()
        {
            try
            {
                using (var myLock = provider.GlobalLock("NastyAPI", TimeSpan.FromMinutes(5.0)))
                {
                    NastyAPI.DoOp();
                } // Lock released when Disposed
            }
            catch (LockUnavailableException)
            {
                // Do nothing
            }
            catch (LockRequestErrorException)
            {
                // Sproc.Lock internal error occurred
            }
        }
    }

What does this do? Well, it gets the lock called ``NastyAPI`` from the Sproc.Lock server if
(and only if) it's available; if it is, it calls NastyAPI.

If not it does nothing.  All ``Lock``s and ``LockResult``s are ``IDisposable``, so the lock will 
be dropped when the function completes regardless of which execution route is taken.

Regardless of what else happens, the lock server will drop the lock after 5 minutes; the
maximum duration specified in the get lock call should be well in excess of the time you
expect NastyAPI to take. Why is this? Well - if your service were to crash, or (worse)
hang indefinitely, that lock would be unavailable forever. Conversely, if you pick too
short a maximum duration your operation may not be finished before the lock expires.

Be conservative with maximum durations; an order of magnitude more than the normal time
required is probably about right.

This is all very clean; but normally you don't want to just "do nothing" if the lock is
unavailable. Let's wait for it to become free instead.

*)

let WaitForNastyData () =
    use lock =
        fun () -> GetGlobalLock lserver (TimeSpan.FromMinutes 5.) "NastyAPI"
        |> AwaitLock (TimeSpan.FromMinutes 5.)
    match lock with
    | Locked l ->
        NastyAPI.DoOp()
    | Unavailable ->
        () // Do nothing
    | Error i ->
        () // Sproc.Lock internal error occurred

(**

    [lang=csharp]
    static public void WaitForNastyData()
    {
        try
        {
            using (var myLock = provider.AwaitGlobalLock("NastyAPI",
                TimeSpan.FromMinutes(5.0), 
                TimeSpan.FromMinutes(5.0)))
            {
                NastyAPI.DoOp();
            } // Lock released when Disposed
        }
        catch (LockUnavailableException)
        {
            // Do nothing
        }
        catch (LockRequestErrorException)
        {
            // Sproc.Lock internal error occurred
        }
    }


This code is very similar to the code above, except that if the lock is not immediately
available, it will check every repeatedly for the next 5 minutes until it is.

Only after the 5 minutes is up will it then report the lock unavailable; if it acquires
it on any of the attempts inbetween it will call NastyAPI.

How often does it poll? Initially very fast, backing off if the lock is not immediately
available.

If we have multiple ``NastyAPI`` accounts, we can also take advantage of that to make
a number of concurrent requests limited to the number of available accounts.

First, we need to create a seq of lock IDs which are unique to each account:
*)

let lockIds = ["NastyAPI1";"NastyAPI2"]

(**

    [lang=csharp]
    static List<String> lockIds = new List<string> { "NastyAPI1", "NastyAPI2" };

Then we can try to see if any of them are available:
*)

let GetAnyNastyData () =
    use lock =
        lockIds
        |> OneOfLocks (fun id -> GetGlobalLock lserver (TimeSpan.FromMinutes 5.) id)
    match lock with
    | Locked l ->
        // We know which lock we obtained here
        NastyAPI.DoAccountOp l.LockId
    | Unavailable ->
        () // Do nothing - no locks available
    | Error i ->
        () // Sproc.Lock internal error occurred

(**

    [lang=csharp]
    static public void GetAnyNastyData()
    {
        try
        {
            using (var myLock = provider.OneOf(
                    id => provider.GlobalLock(id, TimeSpan.FromMinutes(5.0)),
                    lockIds
                ))
            {
                NastyAPI.DoAccountOp(myLock.LockId);
            } // Lock released when Disposed
        }
        catch (LockUnavailableException)
        {
        }
        catch (LockRequestErrorException)
        {
        }
    }


Or, again, we can await one of the collection of locks:

*)

let AwaitAnyNastyData () =
    use lock =
        fun () ->
            lockIds
            |> OneOfLocks (fun id -> GetGlobalLock lserver (TimeSpan.FromMinutes 5.) id)
        |> AwaitLock (TimeSpan.FromMinutes 5.)
    match lock with
    | Locked l ->
        // We know which lock we obtained here
        NastyAPI.DoAccountOp l.LockId
    | Unavailable ->
        () // Do nothing - no locks available
    | Error i ->
        () // Sproc.Lock internal error occurred

(**

    [lang=csharp]
    static public void AwaitAnyNastyData()
    {
        try
        {
            using (var myLock = provider.AwaitOneOf(
                    id => provider.GlobalLock(id, TimeSpan.FromMinutes(5.0)),
                    lockIds,
                    TimeSpan.FromMinutes(5.0)
                ))
            {
                NastyAPI.DoAccountOp(myLock.LockId);
            } // Lock released when Disposed
        }
        catch (LockUnavailableException)
        {
        }
        catch (LockRequestErrorException)
        {
        }
    }


Using "scoped" locks works in a similar fashion. Let's look at an API that a "Organisation" can only access with a single
shared connection:

*)

let OrganisationNastyData orgName =
    use lock =
        GetOrganisationLock lserver orgName (TimeSpan.FromMinutes 5.) "NastyAPI"
    match lock with
    | Locked _ ->
        // Any number of organisations can do this at the same time
        NastyAPI.DoOp ()
    | Unavailable ->
        // Only reached if the same organisation has already acquired the lock
        ()
    | Error i ->
        // Sproc.Lock hit an error
        ()

(**

    [lang=csharp]
    static public void OrganisationNastyData(string orgName)
    {
        try
        {
            using (var myLock = provider.OrganisationLock(orgName, "NastyAPI", TimeSpan.FromMinutes(5.0)))
            {
                NastyAPI.DoOp();
            } // Lock released when Disposed
        }
        catch (LockUnavailableException)
        {
        }
        catch (LockRequestErrorException)
        {
        }
    }


And possibly more frequently, there might be a collection of accounts available within production environments for a client,
and a separate collection for non-production testing.

For our final example, 
let's assume we have multiple accounts available in production, a single testing account, and we're willing to wait up to 2
minutes for a lock to become available.

*)

type NastyAccounts =
    {
        OrganisationName : string
        Environment : string
        AccountNames : string list
    }

let flyAwayProd =
    {
        OrganisationName = "FlyAwayAir"
        Environment = "PRD"
        AccountNames = ["Nasty1";"Nasty2"]
    }

let flyAwayTest =
    {
        OrganisationName = "FlyAwayAir"
        Environment = "TST"
        AccountNames = ["Nasty1"]
    }

let otherTest =
    {
        OrganisationName = "OtherAir"
        Environment = "PRD"
        AccountNames = ["Nasty1"]
    }

let GetNastyLock accounts =
    use lock =
        fun () ->
            accounts.AccountNames
            |> OneOfLocks
                (fun lid ->
                    GetEnvironmentLock
                        lserver
                        accounts.OrganisationName
                        accounts.Environment 
                        (TimeSpan.FromMinutes 5.) lid)
        |> AwaitLock (TimeSpan.FromMinutes 2.)
    match lock with
    | Locked lockId ->
        // All three "Nasty1" locks might be in use concurrently here;
        // but only one from each organisation/environment
        NastyAPI.DoAccountOp lockId
    | Unavailable ->
        ()
    | Error _ ->
        ()

(**

    [lang=csharp]
    public class NastyAccounts
    {
        public string OrganisationName { get; set; }
        public List<string> AccountNames { get; set; }
        public string Environment { get; set; }

        public NastyAccounts(string orgName, string env, List<string> accounts)
        {
            this.OrganisationName = orgName;
            this.Environment = env;
            this.AccountNames = accounts;
        }
    }

    public static NastyAccounts flyAwayProd =
        new NastyAccounts("FlyAwayAir",
            "PRD", new List<string> { "Nasty1", "Nasty2" });

    public static NastyAccounts flyAwayTest =
        new NastyAccounts("FlyAwayAir",
            "TST", new List<string> { "Nasty1" });

    public static NastyAccounts otherTest =
        new NastyAccounts("OtherAir",
            "PRD", new List<string> { "Nasty1" });

    public static void GetNastyLock(NastyAccounts accounts)
    {
        try
        {
            using (var myLock = provider.AwaitOneOf(
                id => provider.EnvironmentLock(
                    id, accounts.OrganisationName, 
                    accounts.Environment, TimeSpan.FromMinutes(5.0)),
                accounts.AccountNames,
                TimeSpan.FromMinutes(2.0)))
            {
                NastyAPI.DoAccountOp(myLock.LockId);
            }
        }
        catch (LockUnavailableException)
        {
        }
        catch (LockRequestErrorException)
        {
        }
    }


*)