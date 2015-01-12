/// A mostly functional discriminated union based API for use from F#
module Sproc.Lock.Fun

open System
open System.Data
open System.Data.SqlClient
open System.Security.Cryptography
open System.Text

let private intervals (timeOut : TimeSpan) =
    let totalMs = timeOut.TotalMilliseconds
    let rec inner polls total =
        seq {
            let x = float polls
            let maxPoll = totalMs / 5.
            let raise y = Math.Pow (y / 10., 4.)
            let next =
                (maxPoll * (raise x)) / (raise x + 1.) |> int
            if next + total > (int totalMs) then
                yield (int totalMs - total)
            else
                yield next
                yield! inner (polls + 1) (total + next)
        }
    inner 2 0 |> Seq.toList

let private AddP<'a> (command : SqlCommand) =
    fun name (value : 'a) ->
        command.Parameters.Add (new SqlParameter(name, value)) |> ignore

let private AddO<'a> (command : SqlCommand) =
    fun name (t : SqlDbType) ->
        let output = SqlParameter(name, t, Direction = ParameterDirection.Output)
        command.Parameters.Add output

type Hashed = Hashed of string

let private (!!>) (str : string) =
    use hash =
        SHA256.Create()
    Encoding.UTF8.GetBytes str
    |> hash.ComputeHash
    |> Convert.ToBase64String

let private (!!) = (!!>) >> Hashed

/// Type representing a Lock that has definitely been acquired. Locks are
/// IDisposable; disposing the lock will ensure it is released.
type Lock =
     /// A lock that applies globally across the lock server
     | Global of connString : string * lockId : string * hashed : Hashed * instance : Guid
     /// A lock scoped to a specific organisation
     | Organisation of connString : string * lockId : string * hashed : Hashed * organisation : Hashed * instance : Guid
     /// A lock scoped to a particular environment belonging to a particular organisation
     | Environment of connString : string * lockId : string * hashed : Hashed * organisation : Hashed * environment : Hashed * instance : Guid
     /// The LockId acquired. Useful in combination when getting one of a list of locks to determine which was free.
     member lock.LockId =
        match lock with
        | Global (_, name, _, _)
        | Organisation (_, name, _, _, _)
        | Environment (_, name, _, _, _, _) ->
            name
     /// Disposing releases the lock
     member lock.Dispose () =
        match lock with
        | Global (connString, _, (Hashed hash), instance) ->
            use conn = new SqlConnection(connString)
            conn.Open()
            use dropDbLock = conn.CreateCommand()
            dropDbLock.CommandType <- CommandType.StoredProcedure
            dropDbLock.CommandText <- "usp_global_droplock"
            AddP dropDbLock "lockId" hash
            AddP dropDbLock "instance" instance
            dropDbLock.ExecuteScalar() |> ignore
        | Organisation (connString, _, (Hashed hash), (Hashed org), instance) ->
            use conn = new SqlConnection(connString)
            conn.Open()
            use dropDbLock = conn.CreateCommand()
            dropDbLock.CommandType <- CommandType.StoredProcedure
            dropDbLock.CommandText <- "usp_organisation_droplock"
            AddP dropDbLock "lockId" hash
            AddP dropDbLock "organisation" org
            AddP dropDbLock "instance" instance
            dropDbLock.ExecuteScalar() |> ignore
        | Environment (connString, _, (Hashed hash), (Hashed org), (Hashed env), instance) ->
            use conn = new SqlConnection(connString)
            conn.Open()
            use dropDbLock = conn.CreateCommand()
            dropDbLock.CommandType <- CommandType.StoredProcedure
            dropDbLock.CommandText <- "usp_environment_droplock"
            AddP dropDbLock "lockId" hash
            AddP dropDbLock "organisation" org
            AddP dropDbLock "environment" env
            AddP dropDbLock "instance" instance
            dropDbLock.ExecuteScalar() |> ignore
     interface IDisposable with
         /// Disposing releases the lock
         member lock.Dispose () =
            lock.Dispose()

/// A type representing the possible results of attempting to acquire a lock.
type LockResult =
     /// A lock was successfully acquired
     | Locked of Lock
     /// No lock was available
     | Unavailable
     /// The attempt to acquire a lock caused an error in SQL Server
     | Error of int
     /// Disposing a lock result disposes the lock if it was acquired, and has no effect otherwise
     member x.Dispose () =
        match x with
        | Locked l -> l.Dispose()
        | Unavailable -> ()
        | Error _ -> ()
     interface IDisposable with
        member x.Dispose () =
            x.Dispose()

/// Attempts to acquire a global lock from the specified server with
/// the specified lockIdentifier. After maxDuration has elapsed the
/// lock will become "stale" and will be automatically released to the
/// next requester.
let GetGlobalLock connString (maxDuration : TimeSpan) lockIdentifier =
    if maxDuration < TimeSpan.Zero then Error -999
    else
        use conn = new SqlConnection(connString)
        conn.Open()
        use getDbLock = conn.CreateCommand()
        let hId = !!> lockIdentifier
        getDbLock.CommandType <- CommandType.StoredProcedure
        getDbLock.CommandText <- "usp_global_createlock"
        AddP getDbLock "lockId" hId
        AddP getDbLock "stale" (maxDuration.TotalMilliseconds)
        let instanceParameter = AddO getDbLock "instance" SqlDbType.UniqueIdentifier
        let result = getDbLock.ExecuteScalar() :?> int
        match result with
        | i when i >= 0 ->
            Locked <| (Global (connString, lockIdentifier, Hashed hId, instanceParameter.Value :?> Guid))
        | i when i = -1 ->
            Unavailable
        | i -> Error i

/// Attempts to acquire an organisation scoped lock from the specified server with
/// the specified lockIdentifier. After maxDuration has elapsed the
/// lock will become "stale" and will be automatically released to the
/// next requester.
let GetOrganisationLock connString organisation (maxDuration : TimeSpan) lockIdentifier =
    if maxDuration < TimeSpan.Zero then Error -999
    else
        use conn = new SqlConnection(connString)
        conn.Open()
        use getDbLock = conn.CreateCommand()
        let hId = !!> lockIdentifier
        let hOrg = !!> organisation
        getDbLock.CommandType <- CommandType.StoredProcedure
        getDbLock.CommandText <- "usp_organisation_createlock"
        AddP getDbLock "lockId" hId
        AddP getDbLock "organisation" hOrg
        AddP getDbLock "stale" (maxDuration.TotalMilliseconds)
        let instanceParameter = AddO getDbLock "instance" SqlDbType.UniqueIdentifier
        let result = getDbLock.ExecuteScalar() :?> int
        match result with
        | i when i >= 0 ->
            Locked <| (Organisation (connString, lockIdentifier, Hashed hId, Hashed hOrg, instanceParameter.Value :?> Guid))
        | i when i = -1 ->
            Unavailable
        | i -> Error i

/// Attempts to acquire an environment scoped lock from the specified server with
/// the specified lockIdentifier. After maxDuration has elapsed the
/// lock will become "stale" and will be automatically released to the
/// next requester.
let GetEnvironmentLock connString organisation environment (maxDuration : TimeSpan) lockIdentifier =
    if maxDuration < TimeSpan.Zero then Error -999
    else
        use conn = new SqlConnection(connString)
        conn.Open()
        use getDbLock = conn.CreateCommand()
        let hId = !!> lockIdentifier
        let hOrg = !!> organisation
        let hEnv = !!> environment
        getDbLock.CommandType <- CommandType.StoredProcedure
        getDbLock.CommandText <- "usp_environment_createlock"
        AddP getDbLock "lockId" hId
        AddP getDbLock "organisation" hOrg
        AddP getDbLock "environment" hEnv
        AddP getDbLock "stale" (maxDuration.TotalMilliseconds)
        let instanceParameter = AddO getDbLock "instance" SqlDbType.UniqueIdentifier
        let result = getDbLock.ExecuteScalar() :?> int
        match result with
        | i when i >= 0 ->
            Locked <| (Environment (connString, lockIdentifier, (Hashed hId), (Hashed hOrg), (Hashed hEnv), instanceParameter.Value :?> Guid))
        | i when i = -1 ->
            Unavailable
        | i -> Error i

/// Drop a lock. Equivilent to calling ``Lock.Dispose()``.
let DropLock (lock : Lock) =
    lock.Dispose()

/// Poll the server waiting for a lock to become available. The method will block for no more than ``timeOut`` time.
let AwaitLock (timeOut : TimeSpan) getLock =
    let rec tryGet pollIntervals =
        async {
            let locked = getLock ()
            match locked with
            | Locked l ->
                return Locked l
            | Unavailable ->
                match pollIntervals with
                | [] ->
                    return Unavailable
                | h::t ->
                    do! Async.Sleep h
                    return! tryGet t
            | Error i ->
                return Error i            
        }
    Async.RunSynchronously(tryGet (intervals timeOut), timeOut.TotalMilliseconds |> int)

let private shuffle xs =
    let rand = Random()
    let arr = xs |> Seq.toArray
    for i in 0..(arr.Length - 1) do
        let j = rand.Next(i, arr.Length)
        let v = arr.[j]
        arr.[j] <- arr.[i]
        arr.[i] <- v
    arr

/// Given a ``getLock`` function of lockId -> LockResult and a list of lockIds,
/// this function will try and acquire any one of the specified lockIds working
/// through them in a random order.
let OneOfLocks getLock lockIds =
    let rec inner getLock lockIds =
        match lockIds with
        | [] ->
            Unavailable
        | h::t ->
            match getLock h with
            | Locked l -> Locked l
            | Unavailable -> inner getLock t
            | Error i -> Error i
    inner getLock (shuffle lockIds |> Array.toList)
