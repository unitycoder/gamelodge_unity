﻿/*	MiniscriptInterpreter.cs

The only class in this file is Interpreter, which is your main interface 
to the MiniScript system.  You give Interpreter some MiniScript source 
code, and tell it where to send its output (via delegate functions called
TextOutputMethod).  Then you typically call RunUntilDone, which returns 
when either the script has stopped or the given timeout has passed.  

For details, see Chapters 1-3 of the MiniScript Integration Guide.
*/

using System;
using System.Collections.Generic;

namespace Miniscript {

	/// <summary>
	/// TextOutputMethod: a delegate used to return text from the script
	/// (e.g. normal output, errors, etc.) to your C# code.
	/// </summary>
	/// <param name="output"></param>
	public delegate void TextOutputMethod(string output);

	/// <summary>
	/// Interpreter: an object that contains and runs one MiniScript script.
	/// </summary>
	public class Interpreter : IDisposable {

		/// <summary>
		/// What made the interpreter stop running?
		/// </summary>
		public enum EndReason
		{
			EndOfFile,
			Yield,
			OutOfTime,
			Error
		}
		
		/// <summary>
		/// standardOutput: receives the output of the "print" intrinsic.
		/// </summary>
		public TextOutputMethod standardOutput {
			get {
				return _standardOutput;
			}
			set {
				_standardOutput = value;
				if (vm != null) vm.standardOutput = value;
			}
		}
		
		/// <summary>
		/// implicitOutput: receives the value of expressions entered when
		/// in REPL mode.  If you're not using the REPL() method, you can
		/// safely ignore this.
		/// </summary>
		public TextOutputMethod implicitOutput;
		
		/// <summary>
		/// errorOutput: receives error messages from the runtime.  (This happens
		/// via the ReportError method, which is virtual; so if you want to catch
		/// the actual exceptions rather than get the error messages as strings,
		/// you can subclass Interpreter and override that method.)
		/// </summary>
		public TextOutputMethod errorOutput;
		
		/// <summary>
		/// hostData is just a convenient place for you to attach some arbitrary
		/// data to the interpreter.  It gets passed through to the context object,
		/// so you can access it inside your custom intrinsic functions.  Use it
		/// for whatever you like (or don't, if you don't feel the need).
		/// </summary>
		public object hostData;
		
		/// <summary>
		/// done: returns true when we don't have a virtual machine, or we do have
		/// one and it is done (has reached the end of its code).
		/// </summary>
		public bool done {
			get { return vm == null || vm.done; }	
		}
		
		/// <summary>
		/// vm: the virtual machine this interpreter is running.  Most applications will
		/// not need to use this, but it's provided for advanced users.
		/// </summary>
		public Machine vm;
		
		TextOutputMethod _standardOutput;
		string source;
        /// <summary>
        /// The parser that we created. If the parser was provided to us in our constructor, then we can simply
        /// leave this as null
        /// </summary>
		Parser parser;
		
		/// <summary>
		/// Constructor taking some MiniScript source code, and the output delegates.
		/// </summary>
		public Interpreter(string source=null, TextOutputMethod standardOutput=null, TextOutputMethod errorOutput=null) {
			this.source = source;
			if (standardOutput == null) standardOutput = Console.WriteLine;
			if (errorOutput == null) errorOutput = Console.WriteLine;
			this.standardOutput = standardOutput;
			this.errorOutput = errorOutput;
		}
		/// <summary>
		/// Constructor taking some parsed code, and the output delegates.
		/// </summary>
		public Interpreter(Parser parser, TextOutputMethod standardOutput=null, TextOutputMethod errorOutput=null) {
            // This parser should already be loaded with code, so we should be able to just use it as-is
            vm = parser.CreateVM(standardOutput);
            vm.interpreter = new WeakReference(this);

            if (standardOutput == null) standardOutput = MiniCompat.Log;
            if (errorOutput == null) errorOutput = MiniCompat.LogError;
			this.standardOutput = standardOutput;
			this.errorOutput = errorOutput;
		}
		
		/// <summary>
		/// Constructor taking source code in the form of a list of strings.
		/// </summary>
		public Interpreter(List<string> source) : this(string.Join("\n", source.ToArray())) {
		}
		
		/// <summary>
		/// Constructor taking source code in the form of a string array.
		/// </summary>
		public Interpreter(string[] source) : this(string.Join("\n", source)) {
		}
		
		/// <summary>
		/// Stop the virtual machine, and jump to the end of the program code.
		/// Also reset the parser, in case it's stuck waiting for a block ender.
		/// </summary>
		public void Stop() {
			if (vm != null) vm.Stop();
			if (parser != null) parser.PartialReset();
		}
		
		/// <summary>
		/// Reset the interpreter with the given source code.
		/// </summary>
		/// <param name="source"></param>
		public void Reset(string source="") {
			this.source = source;
			parser = null;
			vm = null;
		}
		
		/// <summary>
		/// Compile our source code, if we haven't already done so, so that we are
		/// either ready to run, or generate compiler errors (reported via errorOutput).
		/// </summary>
		public void Compile() {
			if (vm != null) return;	// already compiled

			if (parser == null) parser = new Parser();
			try {
				parser.Parse(source);
				vm = parser.CreateVM(standardOutput);
				vm.interpreter = new WeakReference(this);
			} catch (MiniscriptException mse) {
				ReportError(mse);
			}
		}
		
		/// <summary>
		/// Reset the virtual machine to the beginning of the code.  Note that this
		/// does *not* reset global variables; it simply clears the stack and jumps
		/// to the beginning.  Useful in cases where you have a short script you
		/// want to run over and over, without recompiling every time.
		/// </summary>
		public void Restart() {
			if (vm != null) vm.Reset();			
		}
		
		/// <summary>
		/// Run the compiled code until we either reach the end, or we reach the
		/// specified time limit.  In the latter case, you can then call RunUntilDone
		/// again to continue execution right from where it left off.
		/// 
		/// Or, if returnEarly is true, we will also return if we reach an intrinsic
		/// method that returns a partial result, indicating that it needs to wait
		/// for something.  Again, call RunUntilDone again later to continue.
		/// 
		/// Note that this method first compiles the source code if it wasn't compiled
		/// already, and in that case, may generate compiler errors.  And of course
		/// it may generate runtime errors while running.  In either case, these are
		/// reported via errorOutput.
		/// </summary>
		/// <param name="timeLimit">maximum amout of time to run before returning, in seconds</param>
		/// <param name="returnEarly">if true, return as soon as we reach an intrinsic that returns a partial result</param>
		public EndReason RunUntilDone(double timeLimit=60, bool returnEarly=true) {
			try {
				if (vm == null) {
					Compile();
					if (vm == null)
						return EndReason.Error;	// (must have been some error)
				}
				double startTime = vm.runTime;
				vm.yielding = false;
				while (!vm.done && !vm.yielding) {
					if (vm.runTime - startTime > timeLimit)
						return EndReason.OutOfTime;	// time's up for now!
					vm.Step();		// update the machine
					if (returnEarly && vm.GetTopContext().partialResult.IsAssigned)
						return EndReason.Yield;	// waiting for something
				}
			} catch (MiniscriptException mse) {
				ReportError(mse);
				vm.GetTopContext().JumpToEnd();
				return EndReason.Error;
			}
			return EndReason.EndOfFile;
		}
		
		/// <summary>
		/// Run one step of the virtual machine.  This method is not very useful
		/// except in special cases; usually you will use RunUntilDone (above) instead.
		/// </summary>
		public void Step() {
			try {
				Compile();
				vm.Step();
			} catch (MiniscriptException mse) {
				ReportError(mse);
				vm.GetTopContext().JumpToEnd();
			}
		}

		/// <summary>
		/// Read Eval Print Loop.  Run the given source until it either terminates,
		/// or hits the given time limit.  When it terminates, if we have new
		/// implicit output, print that to the implicitOutput stream.
		/// </summary>
		/// <param name="sourceLine">Source line.</param>
		/// <param name="timeLimit">Time limit.</param>
		public void REPL(string sourceLine, double timeLimit=60) {
			if (parser == null) parser = new Parser();
			if (vm == null) {
				vm = parser.CreateVM(standardOutput);
				vm.interpreter = new WeakReference(this);
			}
			if (sourceLine == "#DUMP") {
				vm.DumpTopContext();
				return;
			}
			
			double startTime = vm.runTime;
			int startImpResultCount = vm.globalContext.implicitResultCounter;
			vm.storeImplicit = (implicitOutput != null);

			try {
				if (sourceLine != null) parser.Parse(sourceLine, true);
				if (!parser.NeedMoreInput()) {
					while (!vm.done && !vm.yielding) {
						if (vm.runTime - startTime > timeLimit) return;	// time's up for now!
						vm.Step();
					}
					if (implicitOutput != null && vm.globalContext.implicitResultCounter > startImpResultCount) {

						Value result = vm.globalContext.GetVar(ValVar.implicitResult.identifier);
						if (result != null) {
							implicitOutput.Invoke(result.ToString(vm));
						}
					}
				}

			} catch (MiniscriptException mse) {
				ReportError(mse);
				// Attempt to recover from an error by jumping to the end of the code.
				vm.GetTopContext().JumpToEnd();
			}
		}
		
		/// <summary>
		/// Report whether the virtual machine is still running, that is,
		/// whether it has not yet reached the end of the program code.
		/// </summary>
		/// <returns></returns>
		public bool Running() {
			return vm != null && !vm.done;
		}
		
		/// <summary>
		/// Return whether the parser needs more input, for example because we have
		/// run out of source code in the middle of an "if" block.  This is typically
		/// used with REPL for making an interactive console, so you can change the
		/// prompt when more input is expected.
		/// </summary>
		/// <returns></returns>
		public bool NeedMoreInput() {
			return parser != null && parser.NeedMoreInput();
		}
		
		/// <summary>
		/// Get a value from the global namespace of this interpreter.
		/// </summary>
		/// <param name="varName">name of global variable to get</param>
		/// <returns>Value of the named variable, or null if not found</returns>
		public Value GetGlobalValue(string varName) {
			if (vm == null) return null;
			Context c = vm.globalContext;
			if (c == null) return null;
			try {
				return c.GetVar(varName);
			} catch (UndefinedIdentifierException) {
				return null;
			}
		}
		/// <summary>
		/// Get a value from the global namespace of this interpreter.
		/// </summary>
		/// <param name="varName">name of global variable to get</param>
		/// <returns>Value of the named variable, or null if not found</returns>
		public Value GetGlobalValue(Value val) {
			if (vm == null) return null;
			Context c = vm.globalContext;
			if (c == null) return null;
			try {
				return c.GetVar(val);
			} catch (UndefinedIdentifierException) {
				return null;
			}
		}
		
		/// <summary>
		/// Set a value in the global namespace of this interpreter.
		/// </summary>
		/// <param name="varName">name of global variable to set</param>
		/// <param name="value">value to set</param>
		public void SetGlobalValue(string varName, Value value) {
            if (vm != null)
            {
                value.Ref();
                vm.globalContext.SetVar(varName, value);
            }
		}
		/// <summary>
		/// Set a value in the global namespace of this interpreter.
		/// </summary>
		/// <param name="varName">name of global variable to set</param>
		/// <param name="value">value to set</param>
		public void SetGlobalValue(Value varName, Value value) {
            if (vm != null)
            {
                value.Ref();
                vm.globalContext.SetVar(varName, value);
            }
		}
		
		/// <summary>
		/// Report a MiniScript error to the user.  The default implementation 
		/// simply invokes errorOutput with the error description.  If you want
		/// to do something different, then make an Interpreter subclass, and
		/// override this method.
		/// </summary>
		/// <param name="mse">exception that was thrown</param>
		protected virtual void ReportError(MiniscriptException mse) {
			errorOutput.Invoke(mse.Description());
		}
        public void Dispose()
        {
            if (vm != null)
                vm.Dispose();
            vm = null;
            if (parser != null)
                parser.Dispose();
            parser = null;
        }
	}
}
