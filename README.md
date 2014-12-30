Take a distributed lock in a shared SQL Server database.

In F#:

Example
-------

Take a distributed lock in a shared SQL Server database.

In F#:

    #r "Sproc.Lock.dll"
    open System
    open Sproc.Lock.Fun

    let connString = "sql server connection string"

    let lock1 = GetGlobalLock connString (TimeSpan.FromMinutes 5.) "MyAppLock"

    match lock1 with
    | Locked l ->
        sprintf "I got a lock called %s!" l.LockId
        // (will be "MyAppLock" on this occassion)
    | Unavailable ->
        sprintf "Someone else had the lock already!"
    | Error i ->
        sprintf "Something went wrong - error code: %d" i

    lock1.Dispose()

And C#:

    using System;
    using Sproc.Lock.OO;


    namespace MyApp
    {
        class Thing
        {
            static void DoLockRequiringWork()
            {
                var provider = new LockProvider("sql connection string");
                try
                {
                    using (var lock2 = provider.GlobalLock("MyAppLock", TimeSpan.FromMinutes(5.0))
                    {
                        // If I get here, I've got a lock!                    
                        // Doing stuff!
                    } // Lock released when Disposed
                }
                catch (LockUnavailableException)
                {
                    // Couldn't get the lock                
                    throw;
                }
                catch (LockRequestErrorException)
                {
                    // Getting the lock threw an error
                    throw;
                }
            }
        }
    }

Documentation
-------------

Sproc.Lock is a combination of a managed .net library and a set of SQL Server scripts that
combine to turn SQL Server into a distributed locking server.

This library is only really recommended if you are already using SQL Server, and do not
have a more suitable distibuted locking server already up and running. In that case, Sproc.Lock
can save you the overhead of adding an additional piece of infrastructure to your environment

Find out more at [15below.github.io/Sproc.Lock/](http://15below.github.io/Sproc.Lock/)