namespace FsToolkit.ErrorHandling

open System.Threading.Tasks
open System
open Hopac

[<AutoOpen>]
module JobOptionCE = 

  type JobOptionBuilder() =

    member __.Return (value: 'T) : Job<_ option> =
      job.Return <| option.Return value

    member __.ReturnFrom
        (jobResult: Job<_ option>)
        : Job<_ option> =
      jobResult

    member __.Zero () : Job<_ option> =
      job.Return <| option.Zero ()

    member __.Bind
        (jobResult: Job<_ option>,
         binder: 'T -> Job<_ option>)
        : Job<_ option> =
      job {
        let! result = jobResult
        match result with
        | Some x -> return! binder x
        | None -> return None
      }

    member this.Bind
        (taskResult: unit -> Task<_ option>,
         binder: 'T -> Job<_ option>)
        : Job<_ option> =
      this.Bind(Job.fromTask taskResult, binder)

    member __.Delay
        (generator: unit -> Job<_ option>)
        : Job<_ option> =
      Job.delay generator

    member this.Combine
        (computation1: Job<_ option>,
         computation2: Job<_ option>)
        : Job<_ option> =
      this.Bind(computation1, fun () -> computation2)

    member __.TryWith
        (computation: Job<_ option>,
         handler: System.Exception -> Job<_ option>)
        : Job<_ option> =
      Job.tryWith computation handler

    member __.TryWith
        (computation: unit -> Job<_ option>,
         handler: System.Exception -> Job<_ option>)
        : Job<_ option> =
      Job.tryWithDelay computation handler

    member __.TryFinally
        (computation: Job<_ option>,
         compensation: unit -> unit)
        : Job<_ option> =
      Job.tryFinallyFun computation  compensation

    member __.TryFinally
        (computation: unit -> Job<_ option>,
         compensation: unit -> unit)
        : Job<_ option> =
      Job.tryFinallyFunDelay computation  compensation
      
    member __.Using
        (resource: 'T when 'T :> IDisposable,
         binder: 'T -> Job<_ option>)
        : Job<_ option> =
      job.Using(resource, binder)

    member this.While
        (guard: unit -> bool, computation: Job<_ option>)
        : Job<_ option> =
      if not <| guard () then this.Zero ()
      else this.Bind(computation, fun () -> this.While (guard, computation))

    member this.For
        (sequence: #seq<'T>, binder: 'T -> Job<_ option>)
        : Job<_ option> =
      this.Using(sequence.GetEnumerator (), fun enum ->
        this.While(enum.MoveNext,
          this.Delay(fun () -> binder enum.Current)))

        /// <summary>
        /// Method lets us transform data types into our internal representation. This is the identity method to recognize the self type.
        ///
        /// See https://stackoverflow.com/questions/35286541/why-would-you-use-builder-source-in-a-custom-computation-expression-builder
        /// </summary>
        member inline _.Source(job : Job<_ option>) : Job<_ option> = job

        /// <summary>
        /// Method lets us transform data types into our internal representation.  
        /// </summary>
        member inline _.Source(async : Async<_ option>) : Job<_ option> = async |> Job.fromAsync

        /// <summary>
        /// Method lets us transform data types into our internal representation.  
        /// </summary>
        member inline _.Source(task : Task<_ option>) : Job<_ option> = task |> Job.awaitTask

  let jobOption = JobOptionBuilder() 

[<AutoOpen>]
// Having members as extensions gives them lower priority in
// overload resolution and allows skipping more type annotations.
module JobOptionCEExtensions =

   type JobOptionBuilder with
    /// <summary>
    /// Needed to allow `for..in` and `for..do` functionality
    /// </summary>
    member inline __.Source(s: #seq<_>) = s

    /// <summary>
    /// Method lets us transform data types into our internal representation.
    /// </summary>
    member inline __.Source(r: 't option) = Job.singleton r
    /// <summary>
    /// Method lets us transform data types into our internal representation.
    /// </summary>
    member inline __.Source(a: Job<'t>) = a |> Job.map Some
    /// <summary>
    /// Method lets us transform data types into our internal representation.
    /// </summary>
    member inline __.Source(a: Async<'t>) = a |> Job.fromAsync |> Job.map Some
    /// <summary>
    /// Method lets us transform data types into our internal representation.
    /// </summary>
    member inline __.Source(a: Task<'t>) = a |> Job.awaitTask |> Job.map Some
