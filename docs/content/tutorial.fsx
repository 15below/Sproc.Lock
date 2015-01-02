(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.
#I "../../bin"
module NastyAPI =
    let DoOp config = ()

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
What does this do? Well, it gets the lock called ``NastyAPI`` from the Sproc.Lock server if
(and only if) it's available; if it is, it calls NastyAPI.

If not it does nothing.  Because the ``LockResult`` returned by ``GetGlobalLock``
is ``IDisposable``, the lock will be dropped when the function completes regardless
of which execution route is taken.

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
        |> AwaitLock (TimeSpan.FromMinutes 5.) (TimeSpan.FromMilliseconds 100.)
    match lock with
    | Locked l ->
        NastyAPI.DoOp()
    | Unavailable ->
        () // Do nothing
    | Error i ->
        () // Sproc.Lock internal error occurred

(**
This code is very similar to the code above, except that if the lock is not immediately
available, it will check every 100 milliseconds for the next 5 minutes until it is.

Only after the 5 minutes is up will it then report the lock unavailable; if it acquires
it on any of the attempts inbetween it will call NastyAPI.
*)