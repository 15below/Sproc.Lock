module Sproc.Lock.Properties

open System
open FsCheck
open NUnit.Framework
open Sproc.Lock.Fun
open Sproc.Lock.OO
open Sproc.Lock.Tests

let lockProvider = LockProvider(connString)
let rand = System.Random()

let defaultMaxDuration = TimeSpan.FromMinutes 1.
let defaultAwaitTimeOut = TimeSpan.FromMilliseconds 10.

let lockToResult func =
    fun ts lid ->
        try
            func ts lid |> Locked
        with
        | :? LockUnavailableException -> Unavailable
        | :? LockRequestErrorException as e -> Error (e.LockErrorCode)    

let globalLockFuncs =
    [
        GetGlobalLock connString
        fun ts lid ->
            (fun () -> GetGlobalLock connString ts lid)
            |> AwaitLock (defaultAwaitTimeOut)
        (fun ts lid -> lockProvider.GlobalLock(lid, ts)) |> lockToResult
        (fun ts lid -> lockProvider.AwaitGlobalLock(lid, ts, defaultAwaitTimeOut)) |> lockToResult
    ]

let organisationLockFuncs =
    [
        GetOrganisationLock connString
        fun org ts lid ->
            (fun () -> GetOrganisationLock connString org ts lid)
            |> AwaitLock (defaultAwaitTimeOut)
        fun org -> (fun ts lid -> lockProvider.OrganisationLock(lid, org, ts)) |> lockToResult
        fun org ->
            (fun ts lid -> lockProvider.AwaitOrganisationLock(lid, org, ts, defaultAwaitTimeOut))
            |> lockToResult
    ]

let environmentLockFuncs =
    [
        GetEnvironmentLock connString
        fun org env ts lid ->
            (fun () -> GetEnvironmentLock connString org env ts lid)
            |> AwaitLock (defaultAwaitTimeOut)
        fun org env -> (fun ts lid -> lockProvider.EnvironmentLock(lid, org, env, ts)) |> lockToResult
        fun org env ->
            (fun ts lid -> lockProvider.AwaitEnvironmentLock(lid, org, env, ts, defaultAwaitTimeOut))
            |> lockToResult
    ]

type GetLock = GetLock of (TimeSpan -> string -> LockResult)
type LockId = LockId of string
type LockType =
    | Global
    | Organisation
    | Environment
type LockCommand =
    | Lock
    | Unlock

let commandsLock = new System.Threading.Semaphore(1, 1)

let rec lockedFunc refCell getLock (lock : LockResult) commands =
    async {
        do! Async.Sleep (rand.Next 5)
        match commands with
        | Lock::t ->
            let newLock = getLock ()
            match newLock with
            | Locked _ ->
                return false
            | Unavailable ->
                return! lockedFunc refCell getLock lock t
            | Error _ ->
                return false               
        | Unlock::t ->
            commandsLock.WaitOne() |> ignore
            let count = !refCell
            if count > 1 then
                return false
            else
                refCell := count - 1
                lock.Dispose()
                commandsLock.Release() |> ignore
                return! unlockedFunc refCell getLock (Some lock) t
        | [] ->
            commandsLock.WaitOne() |> ignore
            let pass = !refCell = 1
            refCell := 0
            lock.Dispose()
            commandsLock.Release() |> ignore
            return pass
    }
and unlockedFunc refCell getLock maybeLock commands =
    async {
        do! Async.Sleep (rand.Next 5)
        match commands with
        | Lock::t ->
            let newLock = getLock ()
            match newLock with
            | Locked l ->
                commandsLock.WaitOne() |> ignore
                let count = !refCell
                if count > 0 then
                    return false
                else
                    refCell := count + 1
                    commandsLock.Release() |> ignore
                    return! lockedFunc refCell getLock newLock t
            | Unavailable ->
                return! unlockedFunc refCell getLock (Some newLock) t
            | Error _ ->
                return false
        | Unlock::t ->
            maybeLock |> Option.iter (fun l -> l.Dispose()) 
            return! unlockedFunc refCell getLock maybeLock t
        | [] ->
            commandsLock.WaitOne() |> ignore
            let count = !refCell
            commandsLock.Release() |> ignore
            return count = 0 || count = 1
    }

type LockGenerator =
    static member GetLock () =
        gen {
            let! lockType = Arb.generate<LockType>
            match lockType with
            | Global ->
                let! getLock =
                    List.length globalLockFuncs - 1 |> (fun i -> Gen.choose (0, i))
                    |> Gen.map (fun i -> globalLockFuncs.[i])
                return GetLock getLock
            | Organisation ->
                let! getLockOrg =
                    List.length organisationLockFuncs - 1 |> (fun i -> Gen.choose (0, i))
                    |> Gen.map (fun i -> organisationLockFuncs.[i])
                let! (NonEmptyString org) =
                    Arb.Default.NonEmptyString().Generator
                return GetLock (getLockOrg org)
            | Environment ->
                let! getLockOrg =
                    List.length environmentLockFuncs - 1 |> (fun i -> Gen.choose (0, i))
                    |> Gen.map (fun i -> environmentLockFuncs.[i])
                let! (NonEmptyString org) = Arb.Default.NonEmptyString().Generator
                let! (NonEmptyString env) = Arb.Default.NonEmptyString().Generator
                return GetLock (getLockOrg org env)
        } |> Arb.fromGen
    static member LockId () =
        gen {
            let! (NonEmptyString guid) = Arb.Default.NonEmptyString().Generator
            return LockId guid
        } |> Arb.fromGen

type Specs =
    static member ``Can't get the same lock twice`` (GetLock gl) (LockId lockId) =
        use lock1 = gl defaultMaxDuration lockId
        use lock2 = gl defaultMaxDuration lockId
        match lock2 with
        | Unavailable -> true
        | _ -> false
    static member ``Can get two different locks at the same time`` (GetLock gl) (LockId l1) (LockId l2) =
        if l1 = l2 then true
        else
            use lock1 = gl defaultMaxDuration l1
            use lock2 = gl defaultMaxDuration l2
            (match lock1 with Locked _ -> true | _ -> false) && (match lock2 with Locked _ -> true | _ -> false)
    static member ``Can await lock release`` (GetLock gl) (LockId lockId) =
        use lock1 = gl (TimeSpan.FromMilliseconds 10.) lockId
        use lock2 = AwaitLock (defaultMaxDuration) (fun () -> gl defaultMaxDuration lockId)
        match lock2 with Locked _ -> true | _ -> false
    static member ``Unlocking frees a lock`` (GetLock gl) (LockId lockId) =
        let lock1 = gl defaultMaxDuration lockId
        lock1.Dispose()
        use lock2 = gl defaultMaxDuration lockId
        match lock2 with Locked _ -> true | _ -> false
    static member ``Get all of a list of locks`` (GetLock gl) (ids : LockId list) =
        let idStrs = ids |> Seq.distinct |> List.ofSeq |> List.map (fun (LockId i) -> i)
        let locks =
            [|for i in 1..List.length idStrs -> async { return OneOfLocks (gl defaultMaxDuration) idStrs }|]
            |> Async.Parallel
            |> Async.RunSynchronously
        use ``should be none left`` =
            OneOfLocks (gl defaultMaxDuration) idStrs
        let result =
            locks
            |> Array.forall (function Locked _ -> true | _ -> false)
            |@ "All locked"
            .&.
            match ``should be none left`` with Unavailable -> true | _ -> false
            |@ "None were left"
        locks |> Array.iter (fun l -> l.Dispose())
        result
    static member ``Freeing one of a list of locks should always leave one free`` (GetLock gl) (ids : NonEmptyArray<LockId>) =
        let idStrs =
            ids.Get
            |> Seq.distinct
            |> Seq.map (fun (LockId i) -> i)
            |> List.ofSeq
        let locks =
            [|for i in 1..List.length idStrs -> async { return OneOfLocks (gl defaultMaxDuration) idStrs }|]
            |> Async.Parallel
            |> Async.RunSynchronously
        locks.[rand.Next(0, locks.Length)].Dispose()
        use spare = OneOfLocks (gl defaultMaxDuration) idStrs
        let result =
            match spare with Locked _ -> true | _ -> false
        locks |> Array.iter (fun l -> l.Dispose())
        result
    static member ``Random locking and unlocking the same lock`` (GetLock gl) (LockId lid) (commands1 : LockCommand list) (commands2 : LockCommand list) =
        let count = ref 0
        let processes =
            [|
                unlockedFunc count (fun () -> gl defaultMaxDuration lid) None commands1
                unlockedFunc count (fun () -> gl defaultMaxDuration lid) None commands2
            |]
        let results =
            processes
            |> Async.Parallel
            |> Async.RunSynchronously
            |> Array.reduce (&&)
        results

[<Test>]
let ``Run properties`` () =
    Arb.register<LockGenerator>() |> ignore
    Check.QuickThrowOnFailureAll typeof<Specs>
