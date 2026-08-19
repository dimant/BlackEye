# Protocol Conformance Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bring BlackEye's DPlus and Icom terminal-mode byte layouts and state machines into agreement with `../dstardocs/*.md` and the captures in `dumps/`, so that the network↔radio bridge produces frames a real reflector and a real ID52 will accept.

**Architecture:** No structural change to the four-piece-per-side design (transport → reader → listener → writer). The work is: (1) correct byte offsets and constants in the writers/packet classes, (2) parameterise the four Icom special frames so they carry the live transmission's sequence/number, (3) move radio-bound sequence numbering from enqueue-time to send-time in `DPlusHandler` so filler frames advance the counters, and (4) stop the two receive loops from dying on the first malformed input. A new `BlackEye.Tests` project pins every byte layout against bytes copied out of `dumps/`.

**Tech Stack:** .NET 10 (`net10.0`), C# 14, nullable enabled, implicit usings, TPL Dataflow, `System.IO.Ports` 10.0.11, xUnit 2.9.3 + coverlet.

**Status:** Tasks 0-5 are complete. The solution is on `net10.0` and `BlackEye.Tests` holds 129 passing tests. Tasks 6-11 are not started.

**Spec:**
- `../dstardocs/DPlusProtocol.md` — DPlus (REF/XRF) field-by-field
- `../dstardocs/IcomTerminalMode.md` — Icom terminal serial protocol field-by-field
- `../dstardocs/CLAUDE.md` — cross-document facts and the list of known doc-vs-capture discrepancies
- `dumps/*.txt` — the raw captures; **ground truth**
- `dumps/significant_bytes.txt` — frame flag/sequence bit notes and the five frame variants
- The findings table below is the review this plan implements. Every task cites the finding IDs it closes.

## Global Constraints

- Target framework is `net10.0` for every project. The build must stay at **zero warnings**; the old `NETSDK1138` out-of-support warning is gone and must not come back.
- **Truth ordering when sources disagree:** `dumps/*.txt` > `../dstardocs/*.md` > `BlackEye.Connectivity/Readme.md`. The `Readme.md` in this repo is an unmaintained fork of the dstardocs pair and still carries offset bugs — never reconcile toward it.
- **Icom index convention:** the wire length byte counts from byte 1 onward, and `IcomTerminalReader` strips it, so **code buffer index = wire index − 1**. Every offset quoted from `IcomTerminalMode.md` must be decremented before it goes in an Icom packet class.
- **DPlus index convention:** the length byte counts itself and `DPlusNetworkReader` keeps the whole datagram, so **code buffer index = wire index**.
- Callsign fields are fixed 8 bytes, space padded; the suffix is 4 bytes. A voice payload is exactly 12 bytes: 9 AMBE + 3 slow data.
- Audio is never decoded. AMBE bytes are copied, never interpreted.
- `IcomTerminalEcho` is the only path currently reachable from `Main` and is the validated serial loopback. Do not change its behaviour except where Task 7 requires it.
- Commit after every task. Do not batch tasks into one commit.

---

## Findings Tracking Table

Severity: **C**ritical (wrong bytes on the wire or a dead code path in the primary flow) · **H**igh · **M**edium · **L**ow.

| ID | Sev | Finding | Site | Task | Status |
|----|-----|---------|------|------|--------|
| F01 | C | `WriteHeader` copies `dstarHeader` into itself → `ArgumentException` at runtime, callsigns never populated | `BlackEye.Connectivity/DPlus/DPlusNetworkWriter.cs:81` | 1 | **Fixed** |
| F02 | C | Login buffer is 27 bytes but declares `0x1C` (28); tail is `DV19994`, capture says `DV019994` (missing `0x30`) | `BlackEye.Connectivity/DPlus/DPlusNetworkWriter.cs:30-35` | 2 | **Fixed** |
| F03 | C | EOT frame declares length `0x1D` (29) but is 32 bytes; packet id never gets the `0x40` last-frame bit | `BlackEye.Connectivity/DPlus/DPlusNetworkWriter.cs:121-135`, `BlackEye/DPlusHandler.cs:163` | 3 | **Fixed** |
| F04 | C | Frame payload offsets off by one (payload starts at 17, not 16) → `Data` is 4 bytes, `IsLast()` can never be true, `AmbeAndData` is 13 bytes and throws in `IcomTerminalWriter.WriteFrame` | `BlackEye.Connectivity/DPlus/DPlusFramePacket.cs:5-9` | 4 | **Fixed** |
| F05 | L | `Length`/`Type` read wire offsets on a length-stripped buffer → `Length` returns the type, `Type` returns the first payload byte (currently unused) | `BlackEye.Connectivity/IcomTerminal/IcomTerminalPacket.cs:19-21` | 6 | Not started |
| F06 | L | `IsEotAck()` looks for packet id `0x80`; `23 80` occurs in zero bytes of all eight dumps — dead code | `BlackEye.Connectivity/IcomTerminal/IcomTerminalFrameAck.cs:9-12` | 6 | Not started |
| F07 | H | All four special frames hard-code their sequence/number bytes (`00 00`, and `08 48` for EOT); doc and captures require the live transmission's ids | `BlackEye.Connectivity/IcomTerminal/IcomTerminalWriter.cs:66-111` | 7 | Not started |
| F08 | H | `WriteEmptyVoiceEmptyData` sends `97 CB E5` — zero occurrences across all dumps; both reference apps send `16 29 F5` | `BlackEye.Connectivity/IcomTerminal/IcomTerminalWriter.cs:77-87` | 7 | Not started |
| F09 | M | `IsLast()` tests three zero bytes spanning AMBE and slow data; the real marker is byte 3 bit `0x40` | `BlackEye.Connectivity/IcomTerminal/IcomTerminalFrame.cs:21-29` | 6 | Not started |
| F10 | M | `IsValid()` returns false for a NAK, so the reader drops it and the listener can never learn the header was rejected | `BlackEye.Connectivity/IcomTerminal/IcomTerminalHeaderAck.cs:20` | 6 | Not started |
| F11 | L | `WriteReset` emits 3 × `0xFF`; the doc calls for 5–100 | `BlackEye.Connectivity/IcomTerminal/IcomTerminalWriter.cs:14-19` | 7 | Not started |
| F12 | M | Echo sends Rpt1/Rpt2 in the opposite order to the rs-ms3w capture; the two writers' `byte[] dstarHeader` overloads also expect opposite field orders | `BlackEye/IcomSerialEcho.cs:70`, both `WriteHeader(byte[])` overloads | 7 | Not started |
| F13 | C | `packetId` is never incremented → every DPlus frame carries packet id 0 and the every-20-frames header resend never fires | `BlackEye/DPlusHandler.cs:172-187` | 8 | Not started |
| F14 | H | `var lastHeaderPacket` shadows the field → field stays null, resend loop is dead even once F13 is fixed | `BlackEye/DPlusHandler.cs:211` | 8 | Not started |
| F15 | H | `sequenceId`/`number` are never reset per transmission and increment before the write, so the first frame is `01 01` not `00 00` | `BlackEye/DPlusHandler.cs:261-263,303` | 9 | Not started |
| F16 | H | Sync-frame decision reads `TerminalToDPlus.packetId` — the radio→network counter — instead of the radio-bound frame number | `BlackEye/DPlusHandler.cs:141` | 9 | Not started |
| F17 | H | `emptyFrames` is never reset to 0, so after 100 cumulative fillers every later receive tears down immediately | `BlackEye/DPlusHandler.cs:99,139` | 9 | Not started |
| F18 | M | Teardown sends the empty-voice-last-frame but never the EOT frame; separately, the network EOT flips state to Idle at *enqueue* time so the queued EOT is never sent | `BlackEye/DPlusHandler.cs:125-133,320-327` | 9 | Not started |
| F19 | C | `WriteLogin("")` is built and discarded, mycall is empty, and `state` starts at `Idle` so the `Disconnected`-guarded connect/login path is unreachable; nothing calls `WriteConnect` | `BlackEye/DPlusHandler.cs:40,280-286` | 10 | Not started |
| F20 | H | Radio's Rpt1/Rpt2 (`DIRECT`/`DIRECT`) forwarded verbatim; the reflector verifies mycall and rpt2, so the stream is dropped | `BlackEye/DPlusHandler.cs:211-217` | 10 | Not started |
| F21 | M | Header CRC hard-coded `00 0b` (a constant copied from one capture, not a CRC of its own contents); `IcomTerminalHeader` exposes no CRC accessor | `BlackEye.Connectivity/DPlus/DPlusNetworkWriter.cs:78` | 5 | **Fixed** |
| F22 | H | `ReceivedCallback` is uncaught and `BeginReceive` is only re-armed after it, so one malformed datagram permanently kills all network reception | `BlackEye.Connectivity/UdpConnection.cs:47-59` | 11 | Not started |
| F23 | M | A resync onto a `0x00` length byte makes `buffer[0]` throw and silently kills the background serial reader task | `BlackEye.Connectivity/IcomTerminal/IcomTerminalReader.cs:35-42` | 11 | Not started |
| F24 | L | On a read exception the callback fires twice — once with the `0xFF` filler, once with the partial buffer | `BlackEye.Connectivity/SerialConnection.cs:85-91` | 11 | Not started |
| F25 | L | Client UDP port pinned to 20002; doc says any port (capture used 59095) and binding fails if taken | `BlackEye.Connectivity/UdpConnection.cs:14` | 11 | Not started |
| F26 | H | A header ack arriving outside playback calls `EchoHeader` on an empty queue → `InvalidOperationException: Queue empty`, which propagates out of the reader loop and kills the serial reader. Reachable after any completed playback, since state returns to `Receiving` with the queue empty. Confirmed by probe on 2026-08-17 | `BlackEye/IcomSerialEcho.cs:65-73,139-152` | 11 | Not started |

**Confirmed correct, do not touch:** serial port settings, all Icom header/frame/ack offsets, the DPlus header and frame layouts, ports 20001/20002, ping/pong shapes, the EOT-ack layout, the 12 ms inter-frame pacing, and the `0xC0` frame-type mask (bit `0x20` is unset in every captured frame, so the `0xC0`/`0xE0` disagreement between `IcomTerminalMode.md` and `significant_bytes.txt` is inert).

---

## File Structure

| File | Responsibility | Change |
|---|---|---|
| `BlackEye.Tests/BlackEye.Tests.csproj` | xUnit test project, references both existing projects | Create (Task 0) |
| `BlackEye.Tests/CaptureBytes.cs` | Byte arrays lifted verbatim from `dumps/`, one `static readonly` per captured packet | Create (Task 0) |
| `BlackEye.Tests/DPlusNetworkWriterTests.cs` | Byte-for-byte assertions for the DPlus writer | Exists (Task 0); extended by Tasks 1, 2, 3, 5 |
| `BlackEye.Tests/DPlusPacketTests.cs` | Offsets and `IsLast()` for header/frame packets | Exists (Task 0); extended by Task 4 |
| `BlackEye.Tests/DStarCrcTests.cs` | CRC verified against the radio's own captured header | Create (Task 5) |
| `BlackEye.Tests/IcomTerminalPacketTests.cs` | Icom packet accessors | Exists (Task 0); extended by Task 6 |
| `BlackEye.Tests/IcomTerminalWriterTests.cs` | Icom writer output | Exists (Task 0); extended by Task 7 |
| `BlackEye.Tests/IcomTerminalReaderTests.cs` | Serial framing and resync | Exists (Task 0); extended by Task 11 |
| `BlackEye.Tests/DPlusHandlerTests.cs` | State machine driven through fake `IConnection`s | Create (Task 8) |
| `BlackEye.Tests/Fakes.cs` | `FakeConnection`, `RecordingTerminalListener`, `Wait.Until` | Exists (Task 0) |
| `BlackEye.Connectivity/DStarCrc.cs` | CRC-X25 over a D-STAR RF header, shared by both sides | Create (Task 5) |
| `BlackEye.Connectivity/DPlus/DPlusNetworkWriter.cs` | DPlus bytes out | Modify (Tasks 1,2,3,5) |
| `BlackEye.Connectivity/DPlus/DPlusFramePacket.cs` | DPlus frame accessors | Modify (Task 4) |
| `BlackEye.Connectivity/IcomTerminal/*.cs` | Icom packet accessors and writer | Modify (Tasks 6,7) |
| `BlackEye.Connectivity/GatewayConfig.cs` | Callsigns and reflector identity, replacing hard-coded literals | Create (Task 10) |
| `BlackEye/DPlusHandler.cs` | Both bridge directions | Modify (Tasks 8,9,10) |
| `BlackEye/IcomSerialEcho.cs` | Serial loopback, caller of the changed writer signatures | Modify (Task 7) |
| `BlackEye.Connectivity/UdpConnection.cs`, `SerialConnection.cs`, `IcomTerminal/IcomTerminalReader.cs` | Transports and framing | Modify (Task 11) |

---

## Task 0: .NET 10 migration and baseline test coverage — COMPLETE (2026-08-17)

Done before the conformance work so that every later task has a platform and a
safety net. Nothing in this task changed production behaviour.

**Migration**

- `BlackEye/BlackEye.csproj` and `BlackEye.Connectivity/BlackEye.Connectivity.csproj`
  retargeted `net6.0` → `net10.0`.
- `System.IO.Ports` bumped `7.0.0-preview.7.22375.6` → `10.0.11` (stable).
- `dotnet build` is clean: **0 warnings, 0 errors**. The `NETSDK1138`
  out-of-support warning is gone.
- Only SDK 10.0.105 is installed on this machine and there is no net6.0 runtime,
  so the old target could not have been run here at all.

**Test project**

`BlackEye.Tests` (`net10.0`, xUnit 2.9.3, coverlet.collector), added to
`BlackEye.sln` with project references to both existing projects.

| File | Covers |
|---|---|
| `CaptureBytes.cs` | Every byte array used by the suite, transcribed from `dumps/` with a source comment per field. `Stripped()` converts a wire packet to what the reader hands the Icom packet classes. |
| `Fakes.cs` | `FakeConnection` (recording `IConnection`), `RecordingTerminalListener`, `Wait.Until` polling helper for the timer-driven tests. |
| `LockedStateTests.cs` | All three primitives, the reentrancy the bridge depends on, and a 16-thread race proving only one transition runs. |
| `PingHandlerTests.cs` | Ping on idle, traffic suppressing pings entirely, three timeouts escalating to the error action and stopping the timer, configurable `maxTimeOuts`. |
| `BlockDataReceiverTests.cs` | Chunk reassembly, empty chunks, cancellation stopping the consumer loop. |
| `IcomTerminalReaderTests.cs` | Resync on `0xFF`, reset-burst resync, a whole transmission read back to back, packets split across arbitrary serial chunks, corrupt and unknown packets not breaking the stream. |
| `IcomTerminalPacketTests.cs` | Header/frame/pong/ack accessors at their documented offsets, validity rules, the radio's terminator frame recognised as last. |
| `IcomTerminalWriterTests.cs` | Ping, header vs capture, header offsets, the raw-blob overload, frame layout, the 12-byte payload guard, and the structure of all four special frames. |
| `DPlusNetworkReaderTests.cs` | Length validation, and dispatch of connect/pong/login-ack/nak/EOT-ack/header/frame, plus a whole stream in order. |
| `DPlusPacketTests.cs` | Length semantics, header callsign offsets, the Rpt1/Rpt2 order inversion against Icom, login ack OKRW vs BUSY. |
| `DPlusNetworkWriterTests.cs` | Ping, connect, disconnect, frame vs capture, and all three `WriteFrame` overloads agreeing. |
| `IcomTerminalEchoTests.cs` | The full record-then-replay cycle through fake connections, plus two out-of-state callbacks. |

**Result: 96 tests, all passing, ~8 s.**

```bash
dotnet build          # 0 warnings
dotnet test           # 96 passed
dotnet test --collect:"XPlat Code Coverage" --results-directory TestResults
```

Line coverage after this task — the shape matters more than the number:

| Class | Line coverage | Note |
|---|---|---|
| `PingHandler`, `IcomTerminalReader`, `IcomTerminalWriter`, `DPlusNetworkReader`, `IcomTerminalHeader`, `DPlusHeaderPacket`, `DPlusLoginAckPacket`, `BlockDataReceiver` | 100% | |
| `LockedState` | 95% | Uncovered line is the lost-race branch of the double-checked read |
| `IcomTerminalFrame` | 92% | |
| `IcomTerminalPong`, `IcomTerminalPacket`, `DPlusPacket` | 83-88% | Remainder is `Length`/`Type` (F05), fixed in Task 6 |
| `IcomTerminalEcho` | 74% | Remainder is `Start()`'s infinite loop and the F26 crash paths |
| `IcomTerminalHeaderAck`, `IcomTerminalFrameAck` | 62-64% | Remainder is the NAK path (F10) and dead `IsEotAck` (F06), both Task 6 |
| `DPlusNetworkWriter` | 39% | `WriteLogin`/`WriteHeader`/`WriteFrameEot` deliberately untested — Tasks 1-3 add the failing tests that drive those fixes |
| `DPlusFramePacket` | 30% | Payload accessors deliberately untested — Task 4 |
| `DPlusHandler` (+ both inner classes) | 0% | Tasks 8-10 |
| `SerialConnection`, `UdpConnection`, `Program` | 0% | Need a radio, a socket, and an entry point respectively; `UdpConnection` becomes testable once Task 11 makes the client port injectable |

**Deliberate omission:** the suite asserts only behaviour that is correct today
*and* stays correct after all 26 findings are fixed. It never encodes a known bug,
so the suite is green now and must stay green: each conformance task adds its own
failing test first. Where a fix changes a signature, the task that changes it also
updates the affected baseline tests — Task 7 is the only one where that is more
than a line or two.

---

## Task 1: DPlus header buffer (F01)

**Files:**
- Modify: `BlackEye.Connectivity/DPlus/DPlusNetworkWriter.cs:65-84`
- Modify: `BlackEye.Tests/DPlusNetworkWriterTests.cs`

**Interfaces:**
- Consumes: `CaptureBytes.DPlusHeader`.
- Produces: unchanged public signatures — `WriteHeader(string rpt1, string rpt2, string urcall, string mycall, string suffix, short sessionid)` and `WriteHeader(byte[] dstarHeader, byte sessionIdHigh, byte sessionIdLow)`. The `byte[]` overload's parameter is a 36-byte blob ordered **Rpt2, Rpt1, urcall, mycall, suffix** — the DPlus wire order, which is *not* the Icom order.

- [x] **Step 1: Write the failing test**

Add this to the existing `DPlusNetworkWriterTests` class — drop the namespace and class wrapper shown here, and delete the sentence in its class comment saying `WriteHeader` is deliberately absent:

```csharp
namespace BlackEye.Tests
{
    using BlackEye.Connectivity.DPlus;
    using Xunit;

    public class DPlusNetworkWriterTests
    {
        [Fact]
        public void WriteHeader_MatchesCaptureThroughCallsigns()
        {
            var writer = new DPlusNetworkWriter();

            var actual = writer.WriteHeader(
                rpt1: "AI6VW  D",
                rpt2: "REF030 C",
                urcall: "CQCQCQ  ",
                mycall: "AI6VW   ",
                suffix: "ID52",
                sessionid: 0x7D37);

            Assert.Equal(58, actual.Length);
            // Bytes 56..57 are the CRC and are covered by Task 5.
            Assert.Equal(CaptureBytes.DPlusHeader[0..56], actual[0..56]);
        }
    }
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter WriteHeader_MatchesCaptureThroughCallsigns`
Expected: FAIL with `System.ArgumentException: Destination array was not long enough` thrown from `WriteHeader`.

- [x] **Step 3: Fix the copy target**

In `DPlusNetworkWriter.WriteHeader(byte[] dstarHeader, byte sessionIdHigh, byte sessionIdLow)`, replace:

```csharp
            dstarHeader.CopyTo(dstarHeader, 20);
```

with:

```csharp
            if (dstarHeader.Length != 36)
            {
                throw new ArgumentException($"{nameof(dstarHeader)} must be 36 bytes: rpt2, rpt1, urcall, mycall (8 each) then suffix (4).");
            }

            dstarHeader.CopyTo(buffer, 20);
```

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter WriteHeader_MatchesCaptureThroughCallsigns`
Expected: PASS

- [x] **Step 5: Commit**

```bash
git add BlackEye.Connectivity/DPlus/DPlusNetworkWriter.cs BlackEye.Tests/DPlusNetworkWriterTests.cs
git commit -m "fix(dplus): copy dstar header into the outgoing buffer"
```

---

## Task 2: DPlus login packet (F02)

**Files:**
- Modify: `BlackEye.Connectivity/DPlus/DPlusNetworkWriter.cs:28-42`
- Modify: `BlackEye.Tests/DPlusNetworkWriterTests.cs`

**Interfaces:**
- Consumes: `CaptureBytes.DPlusLogin`.
- Produces: `WriteLogin(string mycall)` — unchanged signature, now returns exactly 28 bytes. Callers must pass an 8-char space-padded callsign; anything shorter leaves the remaining bytes zero, which is what the capture shows.

- [x] **Step 1: Write the failing test**

Add to `DPlusNetworkWriterTests`:

```csharp
        [Fact]
        public void WriteLogin_MatchesCapture()
        {
            var writer = new DPlusNetworkWriter();

            var actual = writer.WriteLogin("AI6VW");

            Assert.Equal(CaptureBytes.DPlusLogin, actual);
            Assert.Equal(actual.Length, actual[0]);
        }
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter WriteLogin_MatchesCapture`
Expected: FAIL — actual is 27 bytes and the tail reads `DV19994`.

- [x] **Step 3: Restore the missing byte**

Replace the buffer literal in `WriteLogin` with:

```csharp
            var buffer = new byte[] {
                0x1C, 0xC0, 0x04, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x44, 0x56, 0x30, 0x31, 0x39, 0x39, 0x39, 0x34
            };
```

(`44 56 30 31 39 39 39 34` = `DV019994`; the `0x30` was missing, which is why the buffer was one byte short of its own length byte.)

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter WriteLogin_MatchesCapture`
Expected: PASS

- [x] **Step 5: Commit**

```bash
git add BlackEye.Connectivity/DPlus/DPlusNetworkWriter.cs BlackEye.Tests/DPlusNetworkWriterTests.cs
git commit -m "fix(dplus): login packet is 28 bytes ending DV019994"
```

---

## Task 3: DPlus EOT frame (F03)

**Files:**
- Modify: `BlackEye.Connectivity/DPlus/DPlusNetworkWriter.cs:121-135`
- Modify: `BlackEye.Tests/DPlusNetworkWriterTests.cs`

**Interfaces:**
- Consumes: `CaptureBytes.DPlusFrame`, `CaptureBytes.DPlusFrameEot`.
- Produces: `WriteFrameEot(short sessionid, byte packetid)` — **the writer now sets bit `0x40` itself**. Callers pass the plain 0..20 packet id, exactly as they do for `WriteFrame`. Task 8 depends on this.

- [x] **Step 1: Write the failing tests**

Add to `DPlusNetworkWriterTests`:

```csharp
        [Fact]
        public void WriteFrame_MatchesCapture()
        {
            var writer = new DPlusNetworkWriter();
            var ambeAndData = CaptureBytes.DPlusFrame[17..29];

            var actual = writer.WriteFrame(ambeAndData, (short)0x7D37, 0x11);

            Assert.Equal(CaptureBytes.DPlusFrame, actual);
            Assert.Equal(actual.Length, actual[0]);
        }

        [Fact]
        public void WriteFrameEot_MatchesCaptureAndSetsLastFrameBit()
        {
            var writer = new DPlusNetworkWriter();

            // 18 is the plain packet id; the writer must emit 0x52 = 0x40 | 18.
            var actual = writer.WriteFrameEot((short)0x7D37, 18);

            Assert.Equal(CaptureBytes.DPlusFrameEot, actual);
            Assert.Equal(actual.Length, actual[0]);
            Assert.Equal(0x40, actual[16] & 0x40);
        }
```

- [x] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter DPlusNetworkWriterTests`
Expected: `WriteFrame_MatchesCapture` PASSES (that layout is already correct); `WriteFrameEot_MatchesCaptureAndSetsLastFrameBit` FAILS on byte 0 (`0x1D` vs `0x20`) and byte 16 (`0x12` vs `0x52`).

- [x] **Step 3: Fix the length byte and set the last-frame bit**

Replace `WriteFrameEot` with:

```csharp
        public byte[] WriteFrameEot(short sessionid, byte packetid)
        {
            byte sessionIdHigh = (byte)(sessionid >> 8);
            byte sessionIdLow = (byte)(sessionid & 0xFF);

            // Bit 0x40 on the packet id marks the last frame of the stream.
            byte lastPacketId = (byte)(packetid | 0x40);

            var buffer = new byte[32]
            {
                0x20, 0x80, 0x44, 0x53, 0x56, 0x54, 0x20, 0x00, 0x00, 0x00, 0x20, 0x00, 0x02, 0x01,
                sessionIdHigh, sessionIdLow, lastPacketId,
                0x9E, 0x8D, 0x32, 0x88, 0x26, 0x1A, 0x3F, 0x61, 0xE8,
                0x55, 0x55, 0x55,
                0x55, 0xC8, 0x7A
            };

            return buffer;
        }
```

- [x] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter DPlusNetworkWriterTests`
Expected: PASS

- [x] **Step 5: Commit**

```bash
git add BlackEye.Connectivity/DPlus/DPlusNetworkWriter.cs BlackEye.Tests/DPlusNetworkWriterTests.cs
git commit -m "fix(dplus): EOT frame length 0x20 and 0x40 last-frame bit"
```

---

## Task 4: DPlus frame payload offsets (F04)

**Files:**
- Modify: `BlackEye.Connectivity/DPlus/DPlusFramePacket.cs:5-9`
- Modify: `BlackEye.Tests/DPlusPacketTests.cs`

**Interfaces:**
- Consumes: `CaptureBytes.DPlusFrame`, `CaptureBytes.DPlusFrameEot`, `CaptureBytes.DPlusHeader`.
- Produces: `DPlusFramePacket.Ambe` (9 bytes), `.Data` (3 bytes), `.AmbeAndData` (12 bytes), `.IsLast()`. Task 9 relies on `AmbeAndData` being exactly 12 bytes so it can hand it to `IcomTerminalWriter.WriteFrame`.

- [x] **Step 1: Write the failing test**

Add these to the existing `DPlusPacketTests` class — drop the wrapper, skip `HeaderCallsignOffsets` (the baseline already has it), and delete the class-comment note saying the frame accessors are deliberately untested:

```csharp
namespace BlackEye.Tests
{
    using BlackEye.Connectivity.DPlus;
    using Xunit;

    public class DPlusPacketTests
    {
        [Fact]
        public void FramePayloadStartsAtSeventeen()
        {
            var packet = new DPlusFramePacket(CaptureBytes.DPlusFrame);

            Assert.Equal(9, packet.Ambe.Length);
            Assert.Equal(3, packet.Data.Length);
            Assert.Equal(12, packet.AmbeAndData.Length);
            Assert.Equal(new byte[] { 0x5b, 0x61, 0x94, 0x4b, 0xd4, 0xe3, 0xe0, 0xa0, 0x6a }, packet.Ambe);
            Assert.Equal(new byte[] { 0x55, 0x55, 0x55 }, packet.Data);
        }

        [Fact]
        public void LastVoiceFrameIsDetected()
        {
            Assert.True(new DPlusFramePacket(CaptureBytes.DPlusFrame).IsLast());
        }

        [Fact]
        public void EotFrameIsDetected()
        {
            Assert.True(new DPlusFramePacket(CaptureBytes.DPlusFrameEot).IsLast());
        }

        [Fact]
        public void HeaderCallsignOffsets()
        {
            var packet = new DPlusHeaderPacket(CaptureBytes.DPlusHeader);

            Assert.Equal("REF030 C", packet.Rpt2);
            Assert.Equal("AI6VW  D", packet.Rpt1);
            Assert.Equal("CQCQCQ  ", packet.UrCall);
            Assert.Equal("AI6VW   ", packet.MyCall);
            Assert.Equal("ID52", packet.Suffix);
        }
    }
}
```

- [x] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter DPlusPacketTests`
Expected: `HeaderCallsignOffsets` PASSES (header offsets are already right). The three frame tests FAIL — `Data` comes back 4 bytes for the normal frame and 7 for the EOT frame, so both `IsLast()` calls return false.

- [x] **Step 3: Shift the payload offsets by one**

In `DPlusFramePacket.cs`, byte 16 is the packet id and the payload is 17..28:

```csharp
        public byte[] Ambe => buffer[17..26];

        public byte[] AmbeAndData => buffer[17..29];

        public byte[] Data => buffer[26..29];
```

(Fixed upper bounds, not open-ended ranges — the EOT frame is 32 bytes and its trailing three bytes are not payload.)

- [x] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter DPlusPacketTests`
Expected: PASS

- [x] **Step 5: Commit**

```bash
git add BlackEye.Connectivity/DPlus/DPlusFramePacket.cs BlackEye.Tests/DPlusPacketTests.cs
git commit -m "fix(dplus): frame payload starts at byte 17, data is 3 bytes"
```

---

## Task 5: D-STAR header CRC (F21)

The reference client emits a constant `00 0b` that is **not** the CRC of its own header — verified: the CRC of that header's contents is `e3 94`. The radio's own header CRC (`58 14`) *is* a valid CRC-X25 over the 39 RF-header bytes, low byte first. Since Task 10 rewrites Rpt1/Rpt2 before transmission, piping the radio's CRC through would ship a stale value, so compute it.

**Files:**
- Create: `BlackEye.Connectivity/DStarCrc.cs`
- Create: `BlackEye.Tests/DStarCrcTests.cs`
- Modify: `BlackEye.Connectivity/DPlus/DPlusNetworkWriter.cs` (`WriteHeader(byte[], byte, byte)`)
- Modify: `BlackEye.Connectivity/IcomTerminal/IcomTerminalHeader.cs`
- Modify: `BlackEye.Tests/DPlusNetworkWriterTests.cs`
- Modify: `BlackEye.Tests/IcomTerminalPacketTests.cs`

**Interfaces:**
- Consumes: `CaptureBytes.IcomHeaderFromRadioWire`.
- Produces: `DStarCrc.Compute(byte[] data, int offset, int count) -> ushort`; `IcomTerminalHeader.Crc -> ushort`, `.RxStatus -> byte`, `.IsCrcValid() -> bool`.

- [x] **Step 1: Write the failing test**

Create `BlackEye.Tests/DStarCrcTests.cs`:

```csharp
namespace BlackEye.Tests
{
    using BlackEye.Connectivity;
    using BlackEye.Connectivity.IcomTerminal;
    using Xunit;

    public class DStarCrcTests
    {
        [Fact]
        public void ComputeMatchesTheRadiosOwnHeaderCrc()
        {
            // Wire bytes 2..40 = 3 flags + 36 callsign bytes = 39 bytes.
            var rfHeader = CaptureBytes.IcomHeaderFromRadioWire[2..41];

            var crc = DStarCrc.Compute(rfHeader, 0, rfHeader.Length);

            // Wire 41 is the low byte, wire 42 the high byte.
            Assert.Equal(0x1458, crc);
            Assert.Equal(0x58, (byte)(crc & 0xFF));
            Assert.Equal(0x14, (byte)(crc >> 8));
        }

        [Fact]
        public void IcomHeaderExposesCrcAndValidatesIt()
        {
            var packet = new IcomTerminalHeader(CaptureBytes.Stripped(CaptureBytes.IcomHeaderFromRadioWire));

            Assert.Equal(0x1458, packet.Crc);
            Assert.Equal(0x00, packet.RxStatus);
            Assert.True(packet.IsCrcValid());
        }
    }
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter DStarCrcTests`
Expected: FAIL to compile — `DStarCrc` and `IcomTerminalHeader.Crc` do not exist.

- [x] **Step 3: Implement the CRC**

Create `BlackEye.Connectivity/DStarCrc.cs`:

```csharp
namespace BlackEye.Connectivity
{
    /// <summary>
    /// CRC-16 X-25 (reflected CCITT: init 0xFFFF, poly 0x8408, final xor 0xFFFF)
    /// over the 39-byte D-STAR RF header (3 flag bytes + 4 callsign fields).
    /// Verified against the ID52's own header CRC in dumps/rs-ms3w-rs232-dump.txt,
    /// which transmits the low byte first.
    /// </summary>
    public static class DStarCrc
    {
        public static ushort Compute(byte[] data, int offset, int count)
        {
            ushort crc = 0xFFFF;

            for (int i = offset; i < offset + count; i++)
            {
                crc ^= data[i];

                for (int bit = 0; bit < 8; bit++)
                {
                    crc = (crc & 1) != 0
                        ? (ushort)((crc >> 1) ^ 0x8408)
                        : (ushort)(crc >> 1);
                }
            }

            return (ushort)(crc ^ 0xFFFF);
        }
    }
}
```

- [x] **Step 4: Expose CRC and rx status on the Icom header**

Add to `IcomTerminalHeader` (stripped indices: flags 1..3, callsigns 4..39, CRC low 40, CRC high 41, rx status 42):

```csharp
        public ushort Crc => (ushort)(buffer[40] | (buffer[41] << 8));

        public byte RxStatus => buffer[42];

        public bool IsCrcValid() => DStarCrc.Compute(buffer, 1, 39) == Crc;
```

Add `using BlackEye.Connectivity;` if the namespace is not already in scope.

- [x] **Step 5: Run test to verify it passes**

Run: `dotnet test --filter DStarCrcTests`
Expected: PASS

- [x] **Step 6: Write the failing test for the DPlus header CRC**

Add to `DPlusNetworkWriterTests`:

```csharp
        [Fact]
        public void WriteHeader_ComputesCrcOverItsOwnContents()
        {
            var writer = new DPlusNetworkWriter();

            var actual = writer.WriteHeader(
                rpt1: "AI6VW  D",
                rpt2: "REF030 C",
                urcall: "CQCQCQ  ",
                mycall: "AI6VW   ",
                suffix: "ID52",
                sessionid: 0x7D37);

            // The capture's 00 0b is a constant, not a CRC of these bytes; e3 94 is.
            Assert.Equal(0xe3, actual[56]);
            Assert.Equal(0x94, actual[57]);
        }
```

- [x] **Step 7: Run test to verify it fails**

Run: `dotnet test --filter WriteHeader_ComputesCrcOverItsOwnContents`
Expected: FAIL — actual bytes are `00 0b`.

- [x] **Step 8: Compute the CRC in the DPlus header writer**

At the end of `WriteHeader(byte[] dstarHeader, byte sessionIdHigh, byte sessionIdLow)`, after the `CopyTo`:

```csharp
            // Flags at 17..19 plus the 36 callsign bytes at 20..55 = the 39-byte
            // RF header the CRC covers. Low byte first, matching the radio.
            ushort crc = DStarCrc.Compute(buffer, 17, 39);
            buffer[56] = (byte)(crc & 0xFF);
            buffer[57] = (byte)(crc >> 8);
```

Add `using BlackEye.Connectivity;` to `DPlusNetworkWriter.cs`.

- [x] **Step 9: Run the whole suite**

Run: `dotnet test`
Expected: PASS. `WriteHeader_MatchesCaptureThroughCallsigns` only asserts bytes 0..55, so it is unaffected.

- [x] **Step 10: Commit**

```bash
git add BlackEye.Connectivity/DStarCrc.cs BlackEye.Connectivity/DPlus/DPlusNetworkWriter.cs \
        BlackEye.Connectivity/IcomTerminal/IcomTerminalHeader.cs BlackEye.Tests/DStarCrcTests.cs \
        BlackEye.Tests/DPlusNetworkWriterTests.cs
git commit -m "feat: compute D-STAR header CRC instead of hard-coding a captured constant"
```

---

## Task 6: Icom packet accessors (F05, F06, F09, F10)

**Files:**
- Modify: `BlackEye.Connectivity/IcomTerminal/IcomTerminalPacket.cs:19-21`
- Modify: `BlackEye.Connectivity/IcomTerminal/IcomTerminalFrameAck.cs:9-12`
- Modify: `BlackEye.Connectivity/IcomTerminal/IcomTerminalFrame.cs:21-29`
- Modify: `BlackEye.Connectivity/IcomTerminal/IcomTerminalHeaderAck.cs:13-26`
- Modify: `BlackEye.Tests/IcomTerminalPacketTests.cs`

**Interfaces:**
- Consumes: `CaptureBytes.IcomHeaderFromRadioWire`, `CaptureBytes.IcomEotFrameFromRadioWire`, `CaptureBytes.Stripped`.
- Produces: `IcomTerminalPacket.Type` (now reads `buffer[0]`); `IcomTerminalPacket.Length` removed; `IcomTerminalFrame.IsLast()` keyed off bit `0x40`; `IcomTerminalHeaderAck.IsValid()` no longer rejects a NAK. Task 7 and Task 9 consume `IsLast()`.

- [ ] **Step 1: Write the failing tests**

Add these to the existing `IcomTerminalPacketTests` class — drop the namespace and class wrapper shown here, and skip `HeaderCallsignOffsets`, which the baseline already covers:

```csharp
namespace BlackEye.Tests
{
    using BlackEye.Connectivity.IcomTerminal;
    using Xunit;

    public class IcomTerminalPacketTests
    {
        [Fact]
        public void TypeReadsTheStrippedBuffersFirstByte()
        {
            var packet = new IcomTerminalHeader(CaptureBytes.Stripped(CaptureBytes.IcomHeaderFromRadioWire));

            Assert.Equal(IcomTerminalPacket.PacketType.HeaderFromSerial, packet.Type);
            Assert.True(packet.IsValid());
        }

        [Fact]
        public void HeaderCallsignOffsets()
        {
            var packet = new IcomTerminalHeader(CaptureBytes.Stripped(CaptureBytes.IcomHeaderFromRadioWire));

            Assert.Equal("DIRECT  ", packet.Rpt1);
            Assert.Equal("DIRECT  ", packet.Rpt2);
            Assert.Equal("CQCQCQ  ", packet.UrCall);
            Assert.Equal("AI6VW   ", packet.MyCall);
            Assert.Equal("ID52", packet.Suffix);
        }

        [Fact]
        public void TerminatorFrameIsDetectedByTheLastFrameBit()
        {
            var packet = new IcomTerminalFrame(CaptureBytes.Stripped(CaptureBytes.IcomEotFrameFromRadioWire));

            Assert.Equal(0x40, packet.FrameType & 0x40);
            Assert.True(packet.IsLast());
        }

        [Fact]
        public void VoiceFrameWithZeroAmbeBytesIsNotTreatedAsLast()
        {
            // Same shape as a real voice frame, but with the three bytes the old
            // heuristic tested (stripped 10, 11, 12) zeroed and no 0x40 bit.
            var wire = new byte[]
            {
                0x10, 0x12, 0x33, 0x0a, 0x4a, 0x7c, 0x1b, 0xef,
                0x20, 0xe6, 0xcb, 0x00, 0x00, 0x00, 0x2d, 0x16,
                0xff
            };

            var packet = new IcomTerminalFrame(CaptureBytes.Stripped(wire));

            Assert.False(packet.IsLast());
        }

        [Fact]
        public void HeaderNakSurvivesValidationSoItCanBeReported()
        {
            var nak = new IcomTerminalHeaderAck(CaptureBytes.Stripped(new byte[] { 0x03, 0x21, 0x01, 0xff }));

            Assert.True(nak.IsValid());
            Assert.False(nak.Ack);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter IcomTerminalPacketTests`
Expected: `TypeReadsTheStrippedBuffersFirstByte` FAILS (`Type` reads `buffer[1]`); `TerminatorFrameIsDetectedByTheLastFrameBit` PASSES by accident (its bytes 10-12 are zero); `VoiceFrameWithZeroAmbeBytesIsNotTreatedAsLast` FAILS; `HeaderNakSurvivesValidationSoItCanBeReported` FAILS.

- [ ] **Step 3: Fix `IcomTerminalPacket`**

The reader strips the length byte, so `buffer[0]` is the type and the wire length is not retained. Replace the two properties with:

```csharp
        /// <summary>
        /// The reader strips the wire length byte, so buffer[0] is the type and
        /// code index = wire index - 1 throughout the Icom packet classes.
        /// </summary>
        public PacketType Type { get { return (PacketType)buffer[0]; } }

        public int PayloadLength { get { return buffer.Length; } }
```

Delete the old `Length` property. Nothing referenced it (verified by grep before starting).

- [ ] **Step 4: Fix `IcomTerminalFrame.IsLast`**

```csharp
        /// <summary>
        /// The radio marks its terminator frame with bit 0x40 in the frame type
        /// nibble: dumps/rs-ms3w-rs232-dump.txt shows 10 12 56 42 55 c8 7a 00...
        /// The old zero-byte test also matched ordinary voice frames.
        /// </summary>
        public bool IsLast()
        {
            return (FrameType & 0x40) == 0x40;
        }
```

- [ ] **Step 5: Fix `IcomTerminalHeaderAck.IsValid`**

Delete the `buffer[1] != 0x00` rejection so a NAK reaches the listener, and check the packet type instead:

```csharp
        public override bool IsValid()
        {
            if (!base.IsValid())
            {
                return false;
            }

            if (!((PacketType)buffer[0] == PacketType.HeaderToSerialAck))
            {
                return false;
            }

            return true;
        }
```

- [ ] **Step 6: Delete `IcomTerminalFrameAck.IsEotAck`**

`23 80` appears in zero bytes across all eight dumps; the ack for the EOT frame is an ordinary `04 23 <seq> 00`. Delete the method (it has no callers).

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test`
Expected: PASS

- [ ] **Step 8: Commit**

```bash
git add BlackEye.Connectivity/IcomTerminal BlackEye.Tests/IcomTerminalPacketTests.cs
git commit -m "fix(icom): correct Type offset, key IsLast off the 0x40 bit, keep NAKs valid"
```

---

## Task 7: Icom special frames carry live ids (F07, F08, F11, F12)

**Files:**
- Modify: `BlackEye.Connectivity/IcomTerminal/IcomTerminalWriter.cs`
- Modify: `BlackEye/IcomSerialEcho.cs:65-94`
- Modify: `BlackEye.Tests/IcomTerminalWriterTests.cs`

**Interfaces:**
- Consumes: `CaptureBytes.IcomEmptyVoiceEmptyDataWire`, `CaptureBytes.IcomFrameEotWire`, `CaptureBytes.IcomHeaderToRadioWire`.
- Produces the new writer signatures Task 9 depends on:
  - `WriteFrameEot(byte sequenceId, byte number)` — sets bit `0x40` on the number byte itself
  - `WriteEmptyVoiceEmptyData(byte sequenceId, byte number)`
  - `WriteEmptyVoiceSyncData(byte sequenceId, byte number)`
  - `WriteEmptyVoiceLastFrame(byte sequenceId, byte number)`
  - `WriteHeader(string rpt1, string rpt2, string urcall, string mycall, string suffix)` — **parameter order changes to rpt1-first** to match `DPlusNetworkWriter.WriteHeader`, and the `byte[] dstarHeader` overload is ordered Rpt1, Rpt2, urcall, mycall, suffix (the Icom wire order, opposite to DPlus).

- [ ] **Step 1: Write the failing tests**

Add to `IcomTerminalWriterTests`:

```csharp
        [Fact]
        public void EmptyVoiceEmptyDataMatchesWhatTheReferenceAppsSend()
        {
            var writer = new IcomTerminalWriter();

            var actual = writer.WriteEmptyVoiceEmptyData(sequenceId: 0x00, number: 0x00);

            Assert.Equal(CaptureBytes.IcomEmptyVoiceEmptyDataWire, actual);
        }

        [Fact]
        public void SpecialFramesCarryTheLiveTransmissionIds()
        {
            var writer = new IcomTerminalWriter();

            var empty = writer.WriteEmptyVoiceEmptyData(sequenceId: 0x2a, number: 0x0b);

            Assert.Equal(0x2a, empty[2]);
            Assert.Equal(0x0b, empty[3]);
        }

        [Fact]
        public void FrameEotMatchesCaptureAndSetsTheLastFrameBit()
        {
            var writer = new IcomTerminalWriter();

            // Capture shows 10 22 08 48 - sequence 8, number 8, bit 0x40 set.
            var actual = writer.WriteFrameEot(sequenceId: 0x08, number: 0x08);

            Assert.Equal(CaptureBytes.IcomFrameEotWire, actual);
        }

        [Fact]
        public void HeaderToRadioMatchesCapture()
        {
            var writer = new IcomTerminalWriter();

            var actual = writer.WriteHeader(
                rpt1: "AI6VW  L",
                rpt2: "AI6VW  G",
                urcall: "AI6VW   ",
                mycall: "AI6VW  G",
                suffix: "    ");

            Assert.Equal(CaptureBytes.IcomHeaderToRadioWire, actual);
        }

        [Fact]
        public void ResetIsLongEnoughToResyncTheRadio()
        {
            var writer = new IcomTerminalWriter();

            // The doc calls for 5-100 bytes of 0xFF.
            Assert.InRange(writer.WriteReset().Length, 5, 100);
            Assert.All(writer.WriteReset(), b => Assert.Equal(0xFF, b));
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter IcomTerminalWriterTests`
Expected: FAIL to compile — the special-frame methods take no arguments and `WriteHeader`'s first parameter is named `rpt2`.

- [ ] **Step 3: Parameterise the special frames**

Replace the four special-frame methods in `IcomTerminalWriter`:

```csharp
        /// <summary>
        /// Empty voice with empty slow data. The 16 29 F5 tail is what both
        /// reference applications send: 97 CB E5 (quoted in IcomTerminalMode.md)
        /// appears in none of the eight captures.
        /// </summary>
        public byte[] WriteEmptyVoiceEmptyData(byte sequenceId, byte number)
        {
            return WriteSpecialFrame(sequenceId, number, 0x16, 0x29, 0xF5);
        }

        public byte[] WriteEmptyVoiceSyncData(byte sequenceId, byte number)
        {
            return WriteSpecialFrame(sequenceId, number, 0x55, 0x2D, 0x16);
        }

        public byte[] WriteEmptyVoiceLastFrame(byte sequenceId, byte number)
        {
            return WriteSpecialFrame(sequenceId, number, 0x55, 0x55, 0x55);
        }

        private byte[] WriteSpecialFrame(byte sequenceId, byte number, byte data0, byte data1, byte data2)
        {
            return new byte[17]
            {
                0x10, 0x22, sequenceId, number,
                0x9E, 0x8D, 0x32, 0x88, 0x26, 0x1A, 0x3F, 0x61, 0xE8,
                data0, data1, data2,
                0xFF
            };
        }

        /// <summary>
        /// The terminating frame. Bit 0x40 on the number byte marks it as last:
        /// the capture shows 10 22 08 48 directly after 10 22 07 07.
        /// </summary>
        public byte[] WriteFrameEot(byte sequenceId, byte number)
        {
            return new byte[17]
            {
                0x10, 0x22, sequenceId, (byte)(number | 0x40),
                0x55, 0xC8, 0x7A,
                0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55,
                0xFF
            };
        }
```

- [ ] **Step 4: Lengthen the reset burst**

```csharp
        public byte[] WriteReset()
        {
            // The doc calls for 5-100 bytes of 0xFF to resync the radio.
            var buffer = new byte[16];
            Array.Fill(buffer, (byte)0xFF);

            return buffer;
        }
```

- [ ] **Step 5: Put `WriteHeader`'s parameters in rpt1-first order**

The field-to-byte mapping is already correct; only the parameter order is a trap. Rename so both writers read the same way, and document the blob order:

```csharp
        public byte[] WriteHeader(string rpt1, string rpt2, string urcall, string mycall, string suffix)
        {
            var rpt1Bytes = Encoding.UTF8.GetBytes(rpt1);
            var rpt2Bytes = Encoding.UTF8.GetBytes(rpt2);
            var urcallBytes = Encoding.UTF8.GetBytes(urcall);
            var mycallBytes = Encoding.UTF8.GetBytes(mycall);
            var suffixBytes = Encoding.UTF8.GetBytes(suffix);

            var dstarHeader = new byte[36];
            rpt1Bytes.CopyTo(dstarHeader, 0);
            rpt2Bytes.CopyTo(dstarHeader, 8);
            urcallBytes.CopyTo(dstarHeader, 16);
            mycallBytes.CopyTo(dstarHeader, 24);
            suffixBytes.CopyTo(dstarHeader, 32);

            return WriteHeader(dstarHeader);
        }

        /// <summary>
        /// dstarHeader is 36 bytes ordered Rpt1, Rpt2, urcall, mycall (8 each),
        /// suffix (4). NOTE: DPlusNetworkWriter's equivalent overload expects
        /// Rpt2 first - the two protocols carry these two fields swapped, so the
        /// blobs are not interchangeable between the two writers.
        /// </summary>
        public byte[] WriteHeader(byte[] dstarHeader)
        {
            if (dstarHeader.Length != 36)
            {
                throw new ArgumentException($"{nameof(dstarHeader)} must be 36 bytes: rpt1, rpt2, urcall, mycall (8 each) then suffix (4).");
            }

            var tag = new byte[] { 0x29, 0x20, 0x01, 0x00, 0x00 };
            var buffer = new byte[42];
            tag.CopyTo(buffer, 0);
            dstarHeader.CopyTo(buffer, 5);
            buffer[41] = 0xff;

            return buffer;
        }
```

- [ ] **Step 6: Update `IcomSerialEcho` for the new signatures**

`EchoHeader` currently passes its two callsigns in the opposite order to the capture. With rpt1-first parameters, pass them so `AI6VW  L` lands at wire byte 5, as rs-ms3w does:

```csharp
        private void EchoHeader()
        {
            var packet = transceiverQueue.Peek();
            if (packet is IcomTerminalHeader)
            {
                var headerBytes = writer.WriteHeader("AI6VW  L", "AI6VW  G", "AI6VW   ", "AI6VW  L", "    ");
                serialConnection.Send(headerBytes);
            }
        }
```

`EchoFrame` must now supply ids for the EOT frame. Add a field next to `state`:

```csharp
        private IcomTerminalFrame? lastEchoedFrame = null;
```

and rewrite `EchoFrame`:

```csharp
        private void EchoFrame()
        {
            var packet = transceiverQueue.Peek();
            if (packet is IcomTerminalFrame)
            {
                var frame = (IcomTerminalFrame)packet;
                if (frame.IsLast())
                {
                    transceiverQueue.Dequeue();

                    // Continue the sequence we were echoing: the capture shows
                    // 10 22 07 07 followed by 10 22 08 48.
                    byte sequenceId = (byte)((lastEchoedFrame?.SequenceId ?? 0) + 1);
                    byte number = (byte)(((lastEchoedFrame?.Number ?? 0) + 1) % 21);

                    var eotBytes = writer.WriteFrameEot(sequenceId, number);
                    serialConnection.Send(eotBytes);
                    state = StateType.Receiving;
                }
                else
                {
                    lastEchoedFrame = frame;
                    var frameBytes = writer.WriteFrame(frame.SequenceId, frame.Number, frame.AmbeAndData);
                    serialConnection.Send(frameBytes);
                }
            }
        }
```

- [ ] **Step 7: Keep `DPlusHandler` compiling**

`DPlusHandler` calls all four changed methods and will not build otherwise. These are stopgaps — Task 9 replaces every one of them with the send-time counters, so do not spend thought on them here.

In `TerminalToDPlus.ReceiveFrame`, pass the existing fields:

```csharp
                        buffer = dplusHandler.terminalWriter.WriteEmptyVoiceLastFrame(0x00, 0x00);
```

```csharp
                            buffer = dplusHandler.terminalWriter.WriteEmptyVoiceSyncData(0x00, 0x00);
                        }
                        else
                        {
                            buffer = dplusHandler.terminalWriter.WriteEmptyVoiceEmptyData(0x00, 0x00);
                        }
```

In `DPlusToTerminal.OnFrame`, pass the counters it already tracks:

```csharp
                        var buffer = dplusHandler.terminalWriter.WriteFrameEot(sequenceId, number);
```

And in `DPlusToTerminal.OnHeader`, swap the two callsign arguments to match the new rpt1-first parameter order (the field-to-byte mapping is unchanged, so this keeps behaviour identical):

```csharp
                    var buffer = dplusHandler.terminalWriter.WriteHeader(
                        packet.Rpt1,
                        packet.Rpt2,
                        packet.UrCall,
                        packet.MyCall,
                        packet.Suffix);
```

- [ ] **Step 7b: Update the baseline writer tests for the new signatures**

Task 0's `IcomTerminalWriterTests` calls the four special frames with no arguments. Two tests need the ids threading through — mechanically, pass `0x00, 0x00` except where the test is about the payload:

- `EveryFrameToTheRadioIsSeventeenBytesAndTerminated` — `WriteFrameEot(0x08, 0x08)`, `WriteEmptyVoiceEmptyData(0x00, 0x00)`, `WriteEmptyVoiceSyncData(0x00, 0x00)`, `WriteEmptyVoiceLastFrame(0x00, 0x00)`
- `EndOfTransmissionFrameCarriesTheDocumentedPayload`, `EndOfTransmissionFrameMarksItselfAsLast` — `WriteFrameEot(0x08, 0x08)`
- `EmptyVoiceFramesShareTheSilentAmbePayload`, `SyncDataFrameCarriesTheSlowDataSyncPattern`, `LastFrameCarriesTheEndOfStreamSlowData` — `(0x00, 0x00)`

`EmptyVoiceEmptyDataMatchesWhatTheReferenceAppsSend` (added in Step 1) then asserts the whole 17-byte packet including the `16 29 f5` tail, which is what makes F08 a real assertion rather than a comment.

- [ ] **Step 8: Run tests and build**

Run: `dotnet build && dotnet test`
Expected: build succeeds with no errors; all tests PASS.

- [ ] **Step 9: Commit**

```bash
git add BlackEye.Connectivity/IcomTerminal/IcomTerminalWriter.cs BlackEye/IcomSerialEcho.cs BlackEye/DPlusHandler.cs BlackEye.Tests/IcomTerminalWriterTests.cs
git commit -m "fix(icom): special frames carry live ids, use the captured empty-data payload"
```

- [ ] **Step 10: Hardware check (requires the radio; skip on macOS/Linux)**

Run `dotnet run --project BlackEye` on Windows against the ID52 on COM4, key up, and confirm the loopback still records and replays a transmission. This is the only regression gate for the echo path.

---

## Task 8: Radio → network packet id and header resend (F13, F14)

**Files:**
- Modify: `BlackEye/DPlusHandler.cs:155-224`
- Create: `BlackEye.Tests/FakeConnection.cs`
- Create: `BlackEye.Tests/DPlusHandlerTests.cs`

**Interfaces:**
- Consumes: `DPlusNetworkWriter.WriteFrameEot(short, byte)` from Task 3 (which now sets bit `0x40`, so pass the plain packet id).
- Produces: `FakeConnection` — an `IConnection` recording every `Send` into `List<byte[]> Sent`. Tasks 9 and 10 reuse it.

- [ ] **Step 1: Write the fake connection**

Create `BlackEye.Tests/FakeConnection.cs`:

```csharp
namespace BlackEye.Tests
{
    using BlackEye.Connectivity;
    using System;
    using System.Collections.Generic;

    public class FakeConnection : IConnection
    {
        public List<byte[]> Sent { get; } = new List<byte[]>();

        public Action<byte[]> ReceivedCallback { private get; set; } = b => { };

        public void Connect() { }

        public void Close() { }

        public void Send(byte[] buffer) => Sent.Add(buffer);
    }
}
```

- [ ] **Step 2: Write the failing test**

Create `BlackEye.Tests/DPlusHandlerTests.cs`:

```csharp
namespace BlackEye.Tests
{
    using BlackEye;
    using BlackEye.Connectivity.DPlus;
    using BlackEye.Connectivity.IcomTerminal;
    using System.Linq;
    using Xunit;

    public class DPlusHandlerTests
    {
        private static IcomTerminalFrame VoiceFrame(byte sequenceId, byte number)
        {
            var wire = new byte[]
            {
                0x10, 0x12, sequenceId, number,
                0xb2, 0x4d, 0x22, 0x48, 0xc0, 0x16, 0x28, 0x26, 0xc8,
                0x16, 0x29, 0xf5,
                0xff
            };

            return new IcomTerminalFrame(CaptureBytes.Stripped(wire));
        }

        private static IcomTerminalHeader RadioHeader()
        {
            return new IcomTerminalHeader(CaptureBytes.Stripped(CaptureBytes.IcomHeaderFromRadioWire));
        }

        [Fact]
        public void OutboundPacketIdCountsAndWrapsAtTwenty()
        {
            var udp = new FakeConnection();
            var serial = new FakeConnection();
            var handler = new DPlusHandler(new DPlusNetworkWriter(), new IcomTerminalWriter(), serial, udp);

            handler.TerminalListener.OnHeader(RadioHeader());
            udp.Sent.Clear();

            for (byte i = 0; i < 22; i++)
            {
                handler.TerminalListener.OnFrame(VoiceFrame(i, i));
            }

            // Frames are the 29-byte packets; byte 16 is the packet id.
            var packetIds = udp.Sent.Where(b => b.Length == 29).Select(b => b[16]).ToArray();

            Assert.Equal(22, packetIds.Length);
            Assert.Equal(0, packetIds[0]);
            Assert.Equal(20, packetIds[20]);
            Assert.Equal(0, packetIds[21]);
        }

        [Fact]
        public void HeaderIsResentEveryTwentyFrames()
        {
            var udp = new FakeConnection();
            var serial = new FakeConnection();
            var handler = new DPlusHandler(new DPlusNetworkWriter(), new IcomTerminalWriter(), serial, udp);

            handler.TerminalListener.OnHeader(RadioHeader());

            // The doc's step 6: no ack for headers, so send a few.
            Assert.Equal(5, udp.Sent.Count(b => b.Length == 58));

            for (byte i = 0; i < 21; i++)
            {
                handler.TerminalListener.OnFrame(VoiceFrame(i, i));
            }

            Assert.Equal(10, udp.Sent.Count(b => b.Length == 58));
        }
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test --filter DPlusHandlerTests`
Expected: FAIL — every packet id is 0, and only the initial 5 headers were sent.

- [ ] **Step 4: Fix the counter and the shadowed field**

In `TerminalToDPlus.OnHeader`, drop the `var` so the field is assigned:

```csharp
                    lastHeaderPacket = dplusHandler.networkWriter.WriteHeader(
                        headerPacket.Rpt1,
                        headerPacket.Rpt2,
                        headerPacket.UrCall,
                        headerPacket.MyCall,
                        headerPacket.Suffix,
                        sessionId);

                    for (int i = 0; i < headerSends; i++)
                    {
                        dplusHandler.udpConnection.Send(lastHeaderPacket);
                    }
```

In `TerminalToDPlus.OnFrame`, send first, then advance, then resend the header on the wrap:

```csharp
                    dplusHandler.state.CompareExecute(TransceiverState_Transmitting, () =>
                    {
                        var buffer = dplusHandler.networkWriter.WriteFrame(framePacket.AmbeAndData, sessionId, packetId);

                        dplusHandler.udpConnection.Send(buffer);

                        packetId = (byte)(packetId >= 20 ? 0 : packetId + 1);

                        // Doc step 8: UDP drops headers, so resend on every wrap.
                        if (packetId == 0 && lastHeaderPacket != null)
                        {
                            for (int i = 0; i < headerSends; i++)
                            {
                                dplusHandler.udpConnection.Send(lastHeaderPacket);
                            }
                        }
                    });
```

The EOT branch passes the plain packet id — `WriteFrameEot` sets bit `0x40` itself as of Task 3:

```csharp
                        var buffer = dplusHandler.networkWriter.WriteFrameEot(sessionId, packetId);
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test --filter DPlusHandlerTests`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add BlackEye/DPlusHandler.cs BlackEye.Tests/FakeConnection.cs BlackEye.Tests/DPlusHandlerTests.cs
git commit -m "fix(bridge): advance the DPlus packet id and resend the header on wrap"
```

---

## Task 9: Network → radio sequencing and teardown (F15, F16, F17, F18)

The root cause of all four is that ids are stamped when a frame is **enqueued** while filler frames are emitted when one is **dequeued**, so fillers do not advance the counters and the two directions read each other's state. Move the numbering to send-time and queue only the payload.

**Files:**
- Modify: `BlackEye/DPlusHandler.cs:42, 83-153, 255-345`
- Modify: `BlackEye.Tests/DPlusHandlerTests.cs`

**Interfaces:**
- Consumes: `IcomTerminalWriter.WriteEmptyVoice*(byte, byte)` and `WriteFrameEot(byte, byte)` from Task 7; `DPlusFramePacket.AmbeAndData` (12 bytes) from Task 4.
- Produces: `DPlusHandler.QueuedFrame` (private nested record); `TerminalToDPlus.BeginReceiveStream()` — called by `DPlusToTerminal.OnHeader` to reset the radio-bound counters. Task 10 calls neither.

- [ ] **Step 1: Write the failing tests**

Add to `DPlusHandlerTests`:

```csharp
        private static DPlusFramePacket NetworkFrame(byte packetId)
        {
            var frame = (byte[])CaptureBytes.DPlusFrame.Clone();
            frame[16] = packetId;
            // Ordinary voice data, not the 55 55 55 end marker.
            frame[26] = 0x16;
            frame[27] = 0x29;
            frame[28] = 0xf5;

            return new DPlusFramePacket(frame);
        }

        private static IcomTerminalFrameAck FrameAck(byte packetId)
        {
            return new IcomTerminalFrameAck(
                CaptureBytes.Stripped(new byte[] { 0x04, 0x23, packetId, 0x00, 0xff }));
        }

        [Fact]
        public void FirstRadioBoundFrameIsNumberedZero()
        {
            var udp = new FakeConnection();
            var serial = new FakeConnection();
            var handler = new DPlusHandler(new DPlusNetworkWriter(), new IcomTerminalWriter(), serial, udp);

            handler.DPlusListener.OnHeader(new DPlusHeaderPacket(CaptureBytes.DPlusHeader));
            handler.DPlusListener.OnFrame(NetworkFrame(0x11));
            serial.Sent.Clear();

            handler.TerminalListener.OnFrameAck(FrameAck(0x00));

            var frame = Assert.Single(serial.Sent);
            Assert.Equal(0x22, frame[1]);
            Assert.Equal(0x00, frame[2]);   // sequence id
            Assert.Equal(0x00, frame[3]);   // number
        }

        [Fact]
        public void FillerFramesAdvanceTheCountersToo()
        {
            var udp = new FakeConnection();
            var serial = new FakeConnection();
            var handler = new DPlusHandler(new DPlusNetworkWriter(), new IcomTerminalWriter(), serial, udp);

            handler.DPlusListener.OnHeader(new DPlusHeaderPacket(CaptureBytes.DPlusHeader));
            serial.Sent.Clear();

            // No network frames queued: three acks produce three fillers.
            handler.TerminalListener.OnFrameAck(FrameAck(0x00));
            handler.TerminalListener.OnFrameAck(FrameAck(0x01));
            handler.TerminalListener.OnFrameAck(FrameAck(0x02));

            Assert.Equal(new byte[] { 0x00, 0x01, 0x02 }, serial.Sent.Select(b => b[2]).ToArray());
            Assert.Equal(new byte[] { 0x00, 0x01, 0x02 }, serial.Sent.Select(b => b[3]).ToArray());
        }

        [Fact]
        public void NetworkEndOfTransmissionReachesTheRadio()
        {
            var udp = new FakeConnection();
            var serial = new FakeConnection();
            var handler = new DPlusHandler(new DPlusNetworkWriter(), new IcomTerminalWriter(), serial, udp);

            handler.DPlusListener.OnHeader(new DPlusHeaderPacket(CaptureBytes.DPlusHeader));
            handler.DPlusListener.OnFrame(new DPlusFramePacket(CaptureBytes.DPlusFrameEot));
            serial.Sent.Clear();

            handler.TerminalListener.OnFrameAck(FrameAck(0x00));

            var eot = Assert.Single(serial.Sent);
            Assert.Equal(0x22, eot[1]);
            Assert.Equal(0x40, eot[3] & 0x40);
            Assert.Equal(0x55, eot[4]);
            Assert.Equal(0xc8, eot[5]);
            Assert.Equal(0x7a, eot[6]);
        }

        [Fact]
        public void ASecondStreamStartsFromZeroAgain()
        {
            var udp = new FakeConnection();
            var serial = new FakeConnection();
            var handler = new DPlusHandler(new DPlusNetworkWriter(), new IcomTerminalWriter(), serial, udp);

            handler.DPlusListener.OnHeader(new DPlusHeaderPacket(CaptureBytes.DPlusHeader));
            handler.TerminalListener.OnFrameAck(FrameAck(0x00));
            handler.TerminalListener.OnFrameAck(FrameAck(0x01));
            handler.DPlusListener.OnFrame(new DPlusFramePacket(CaptureBytes.DPlusFrameEot));
            handler.TerminalListener.OnFrameAck(FrameAck(0x02));

            handler.DPlusListener.OnHeader(new DPlusHeaderPacket(CaptureBytes.DPlusHeader));
            serial.Sent.Clear();
            handler.TerminalListener.OnFrameAck(FrameAck(0x00));

            var frame = Assert.Single(serial.Sent);
            Assert.Equal(0x00, frame[2]);
            Assert.Equal(0x00, frame[3]);
        }
```

Note: these tests exercise `ReceiveFrame`, which sleeps 12 ms per call — the suite stays under a second.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter DPlusHandlerTests`
Expected: FAIL — first frame is numbered `01 01`, fillers all carry `00 00`, and the network EOT never reaches the radio because the state flipped to Idle at enqueue time.

- [ ] **Step 3: Queue payloads instead of finished packets**

Change the queue field on `DPlusHandler` and add the record:

```csharp
        private ConcurrentQueue<QueuedFrame> terminalConnectionQueue = new ConcurrentQueue<QueuedFrame>();

        /// <summary>
        /// A radio-bound frame waiting for the radio to ack the previous one.
        /// Sequence id and number are stamped at send time, not here, so that
        /// filler frames advance the same counters.
        /// </summary>
        private record QueuedFrame(byte[]? AmbeAndData, bool IsEot);
```

- [ ] **Step 4: Move the counters to the sending side**

In `TerminalToDPlus`, replace the `emptyFrames` field block with:

```csharp
            private const int MaxEmptyFrames = 100;

            private int emptyFrames = 0;

            // Radio-bound counters. They live here because this is the class that
            // actually writes to the serial port, on the radio's frame acks.
            private byte txSequenceId = 0;

            private byte txNumber = 0;

            /// <summary>
            /// Called when a network stream starts, before the header goes out.
            /// </summary>
            public void BeginReceiveStream()
            {
                txSequenceId = 0;
                txNumber = 0;
                emptyFrames = 0;
                dplusHandler.terminalConnectionQueue.Clear();
            }

            private void AdvanceTxCounters()
            {
                txSequenceId++;                                            // wraps at 255
                txNumber = (byte)(txNumber >= 20 ? 0 : txNumber + 1);      // 0..20
            }
```

- [ ] **Step 5: Rewrite `ReceiveFrame`**

```csharp
            private void ReceiveFrame()
            {
                Thread.Sleep(FrameSleepMs);

                dplusHandler.state.CompareExecute(TransceiverState_Receiving, () =>
                {
                    byte[] buffer;

                    if (dplusHandler.terminalConnectionQueue.TryDequeue(out var queued))
                    {
                        emptyFrames = 0;

                        if (queued.IsEot)
                        {
                            SendAndGoIdle(dplusHandler.terminalWriter.WriteFrameEot(txSequenceId, txNumber));
                            return;
                        }

                        buffer = dplusHandler.terminalWriter.WriteFrame(txSequenceId, txNumber, queued.AmbeAndData!);
                    }
                    else
                    {
                        emptyFrames++;

                        if (emptyFrames > MaxEmptyFrames)
                        {
                            // The doc: the empty-voice last frame is followed by
                            // the end of transmission frame.
                            dplusHandler.terminalConnection.Send(
                                dplusHandler.terminalWriter.WriteEmptyVoiceLastFrame(txSequenceId, txNumber));
                            AdvanceTxCounters();
                            SendAndGoIdle(dplusHandler.terminalWriter.WriteFrameEot(txSequenceId, txNumber));
                            return;
                        }

                        buffer = txNumber == 0
                            ? dplusHandler.terminalWriter.WriteEmptyVoiceSyncData(txSequenceId, txNumber)
                            : dplusHandler.terminalWriter.WriteEmptyVoiceEmptyData(txSequenceId, txNumber);
                    }

                    dplusHandler.terminalConnection.Send(buffer);
                    AdvanceTxCounters();
                });
            }

            private void SendAndGoIdle(byte[] buffer)
            {
                dplusHandler.terminalConnection.Send(buffer);
                emptyFrames = 0;

                // LockedState uses a Monitor, which is reentrant on this thread,
                // so transitioning inside the enclosing CompareExecute is safe.
                dplusHandler.state.ExchangeExecute(TransceiverState_Idle, () => { });
            }
```

- [ ] **Step 6: Enqueue payloads and reset on the header**

In `DPlusToTerminal`, delete the `sequenceId` and `number` fields, then:

```csharp
            public void OnHeader(DPlusHeaderPacket packet)
            {
                pingHandler.Pong();

                dplusHandler.state.CompareExchangeExecute(TransceiverState_Idle, TransceiverState_Receiving, () =>
                {
                    dplusHandler.terminalToDPlus.BeginReceiveStream();

                    var buffer = dplusHandler.terminalWriter.WriteHeader(
                        packet.Rpt1,
                        packet.Rpt2,
                        packet.UrCall,
                        packet.MyCall,
                        packet.Suffix);

                    dplusHandler.terminalConnection.Send(buffer);
                });
            }

            public void OnFrame(DPlusFramePacket packet)
            {
                pingHandler.Pong();

                dplusHandler.state.CompareExecute(TransceiverState_Receiving, () =>
                {
                    // The state only changes once the EOT has actually gone out
                    // to the radio - see TerminalToDPlus.SendAndGoIdle.
                    dplusHandler.terminalConnectionQueue.Enqueue(
                        packet.IsLast()
                            ? new QueuedFrame(null, true)
                            : new QueuedFrame(packet.AmbeAndData, false));
                });
            }
```

Note `WriteHeader`'s argument order is now rpt1-first, matching Task 7's signature.

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test`
Expected: PASS

- [ ] **Step 8: Commit**

```bash
git add BlackEye/DPlusHandler.cs BlackEye.Tests/DPlusHandlerTests.cs
git commit -m "fix(bridge): stamp radio-bound frame ids at send time and finish teardown"
```

---

## Task 10: Connect, login, and reflector routing (F19, F20)

**Files:**
- Create: `BlackEye.Connectivity/GatewayConfig.cs`
- Modify: `BlackEye/DPlusHandler.cs:28-73, 202-224, 280-297`
- Modify: `BlackEye.Tests/DPlusHandlerTests.cs`

**Interfaces:**
- Consumes: `DPlusNetworkWriter.WriteConnect()`, `.WriteLogin(string)` from Task 2.
- Produces: `GatewayConfig(string MyCall, string Suffix, string ReflectorCall, char ReflectorModule, char RadioModule)` with computed `Rpt1` and `Rpt2`; `DPlusHandler(DPlusNetworkWriter, IcomTerminalWriter, IConnection, IConnection, GatewayConfig)` — the config is a new required constructor parameter; `DPlusHandler.Connect()`.

- [ ] **Step 1: Write the failing tests**

Add to `DPlusHandlerTests` (and update the existing tests' constructor calls to pass `GatewayConfig.Default`):

```csharp
        [Fact]
        public void ConnectSendsTheConnectPacketAndLoginFollowsTheAck()
        {
            var udp = new FakeConnection();
            var serial = new FakeConnection();
            var config = new GatewayConfig("AI6VW", "ID52", "REF030", 'C', 'D');
            var handler = new DPlusHandler(new DPlusNetworkWriter(), new IcomTerminalWriter(), serial, udp, config);

            handler.Connect();

            Assert.Equal(new byte[] { 0x05, 0x00, 0x18, 0x00, 0x01 }, Assert.Single(udp.Sent));

            udp.Sent.Clear();
            handler.DPlusListener.OnConnectAck();

            var login = Assert.Single(udp.Sent);
            Assert.Equal(CaptureBytes.DPlusLogin, login);
        }

        [Fact]
        public void OutboundHeaderCarriesTheReflectorNotTheRadiosDirectCallsigns()
        {
            var udp = new FakeConnection();
            var serial = new FakeConnection();
            var config = new GatewayConfig("AI6VW", "ID52", "REF030", 'C', 'D');
            var handler = new DPlusHandler(new DPlusNetworkWriter(), new IcomTerminalWriter(), serial, udp, config);

            handler.Connect();
            handler.DPlusListener.OnConnectAck();
            handler.DPlusListener.OnLoginAck(new DPlusLoginAckPacket(
                new byte[] { 0x08, 0xC0, 0x04, 0x00, (byte)'O', (byte)'K', (byte)'R', (byte)'W' }));
            udp.Sent.Clear();

            // The radio sends DIRECT / DIRECT in terminal mode.
            handler.TerminalListener.OnHeader(RadioHeader());

            var header = udp.Sent.First(b => b.Length == 58);
            Assert.Equal("REF030 C", Encoding.UTF8.GetString(header[20..28]));
            Assert.Equal("AI6VW  D", Encoding.UTF8.GetString(header[28..36]));
            Assert.Equal("CQCQCQ  ", Encoding.UTF8.GetString(header[36..44]));
            Assert.Equal("AI6VW   ", Encoding.UTF8.GetString(header[44..52]));
        }
```

Add `using System.Text;` and `using BlackEye.Connectivity;` to the test file.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter DPlusHandlerTests`
Expected: FAIL to compile — `GatewayConfig` and `DPlusHandler.Connect` do not exist.

- [ ] **Step 3: Add the config type**

Create `BlackEye.Connectivity/GatewayConfig.cs`:

```csharp
namespace BlackEye.Connectivity
{
    /// <summary>
    /// The identities the gateway puts on the air. In terminal mode the radio
    /// sends DIRECT / DIRECT in its Rpt1 and Rpt2 fields, but a DPlus reflector
    /// verifies mycall and rpt2 before letting a transmission through, so the
    /// gateway substitutes these before forwarding.
    /// </summary>
    public record GatewayConfig(
        string MyCall,
        string Suffix,
        string ReflectorCall,
        char ReflectorModule,
        char RadioModule)
    {
        public static GatewayConfig Default { get; } =
            new GatewayConfig("AI6VW", "ID52", "REF030", 'C', 'D');

        /// <summary>e.g. "AI6VW   " - 8 chars, space padded.</summary>
        public string PaddedMyCall => MyCall.PadRight(8);

        /// <summary>e.g. "AI6VW  D" - callsign in 7, gateway module in the 8th.</summary>
        public string Rpt1 => MyCall.PadRight(7) + RadioModule;

        /// <summary>e.g. "REF030 C" - reflector in 7, module in the 8th.</summary>
        public string Rpt2 => ReflectorCall.PadRight(7) + ReflectorModule;

        public string UrCall => "CQCQCQ  ";

        public string PaddedSuffix => Suffix.PadRight(4);
    }
}
```

- [ ] **Step 4: Thread the config through `DPlusHandler` and add `Connect`**

Add the field and constructor parameter:

```csharp
        private GatewayConfig config;

        public DPlusHandler(
            DPlusNetworkWriter networkWriter,
            IcomTerminalWriter terminalWriter,
            IConnection terminalConnection,
            IConnection udpConnection,
            GatewayConfig config)
        {
            ...
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            ...
        }

        /// <summary>
        /// Doc step 1: the client opens with a connect request. The server echoes
        /// it back, which arrives as OnConnectAck.
        /// </summary>
        public void Connect()
        {
            state.ExchangeExecute(TransceiverState_Disconnected, () =>
                udpConnection.Send(networkWriter.WriteConnect()));
        }
```

Change the initial state so the connect handshake is reachable:

```csharp
        private LockedState state = new LockedState(TransceiverState_Disconnected);
```

- [ ] **Step 5: Actually send the login**

```csharp
            public void OnConnectAck()
            {
                dplusHandler.state.CompareExecute(TransceiverState_Disconnected, () =>
                {
                    var buffer = dplusHandler.networkWriter.WriteLogin(dplusHandler.config.MyCall);

                    dplusHandler.udpConnection.Send(buffer);
                });
            }
```

- [ ] **Step 6: Substitute the routing callsigns on the outbound header**

In `TerminalToDPlus.OnHeader`, keep the radio's urcall (so the operator can still route to a specific station) but replace the repeater fields and mycall identity:

```csharp
                    lastHeaderPacket = dplusHandler.networkWriter.WriteHeader(
                        dplusHandler.config.Rpt1,
                        dplusHandler.config.Rpt2,
                        headerPacket.UrCall,
                        dplusHandler.config.PaddedMyCall,
                        dplusHandler.config.PaddedSuffix,
                        sessionId);
```

- [ ] **Step 7: Update `TerminalToDPlus.OnPong`/`OnFrameAck` guards if the initial state broke them**

`TransceiverState_Idle` is now only reached after a successful login, which is correct per the doc's high-level sequence. Confirm the tests from Tasks 8 and 9 still drive the handler through `Connect()` → `OnConnectAck()` → `OnLoginAck()` before sending frames; update them if they assumed the old Idle-at-construction behaviour.

- [ ] **Step 8: Run the whole suite**

Run: `dotnet test`
Expected: PASS

- [ ] **Step 9: Commit**

```bash
git add BlackEye.Connectivity/GatewayConfig.cs BlackEye/DPlusHandler.cs BlackEye.Tests/DPlusHandlerTests.cs
git commit -m "feat(bridge): send connect/login and route via the configured reflector"
```

---

## Task 11: Transport and reader survival (F22, F23, F24, F25, F26)

**Files:**
- Modify: `BlackEye.Connectivity/UdpConnection.cs`
- Modify: `BlackEye.Connectivity/SerialConnection.cs:71-93`
- Modify: `BlackEye.Connectivity/IcomTerminal/IcomTerminalReader.cs:26-42`
- Modify: `BlackEye/IcomSerialEcho.cs:65-94,113-126,139-152`
- Modify: `BlackEye.Tests/IcomTerminalReaderTests.cs`
- Modify: `BlackEye.Tests/IcomTerminalEchoTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `UdpConnection(string hostname, int clientPort = 0)` — 0 means "let the OS pick", matching the capture's ephemeral 59095.

- [ ] **Step 1: Write the failing test for the reader**

Add to `IcomTerminalReaderTests`:

```csharp
        [Fact]
        public void ReaderSurvivesAZeroLengthByte()
        {
            var listener = new RecordingTerminalListener();
            var reader = new IcomTerminalReader(listener);
            var block = new System.Threading.Tasks.Dataflow.BufferBlock<byte>();

            // Terminator, a bogus zero length byte, then a whole well-formed pong.
            // Note the stream must contain complete packets: Receive() parks on
            // block.Receive() forever if a packet is short.
            foreach (var b in new byte[] { 0xff, 0x00, 0xff, 0x03, 0x03, 0x00, 0xff })
            {
                block.Post(b);
            }

            reader.Receive(block);   // must not throw on the zero length
            reader.Receive(block);

            Assert.Equal(1, listener.Pongs.Count);
            Assert.Equal(1, listener.Ignores);
        }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter ReaderSurvivesAZeroLengthByte`
Expected: FAIL with `IndexOutOfRangeException` from `buffer[0]`.

- [ ] **Step 3: Guard the reader**

In `IcomTerminalReader.Receive`, after filling the buffer:

```csharp
                if (len == 0)
                {
                    // Resync landed on a stray 0x00; drop it and wait for the
                    // next packet rather than indexing an empty buffer.
                    controllerListener.OnIgnore();
                    return;
                }
```

Place the check immediately after the resync loop, before allocating `buffer`.

- [ ] **Step 4: Keep the UDP receive loop alive**

Rewrite `UdpConnection.DataReceived` so the callback can never stop the loop, and re-arm in a `finally`:

```csharp
        private void DataReceived(IAsyncResult ar)
        {
            UdpClient? client = (UdpClient?)ar.AsyncState;

            try
            {
                IPEndPoint? receivedIpEndPoint = new IPEndPoint(IPAddress.Any, serverPort);
                byte[]? data = client?.EndReceive(ar, ref receivedIpEndPoint);

                if (data != null)
                {
                    this.ReceivedCallback?.Invoke(data);
                }
            }
            catch (ObjectDisposedException)
            {
                // Socket closed; stop re-arming.
                return;
            }
            catch
            {
                // A malformed datagram must not kill the receive loop.
            }

            try
            {
                client?.BeginReceive(DataReceived, ar.AsyncState);
            }
            catch (ObjectDisposedException)
            {
            }
        }
```

- [ ] **Step 5: Let the OS pick the client port**

```csharp
        private int clientPort;

        public UdpConnection(string hostname, int clientPort = 0)
        {
            this.hostname = hostname ?? throw new ArgumentNullException(nameof(hostname));
            this.clientPort = clientPort;
        }
```

`new UdpClient(0)` binds an ephemeral port, which is what the capture shows (59095).

- [ ] **Step 6: Stop the double callback on a serial read error**

In `SerialConnection.OnDataReceived`, return after the error path instead of falling through:

```csharp
                    try
                    {
                        this.serialPort.Read(buffer, 0, count);
                    }
                    catch
                    {
                        cancellationTokenSource.Cancel();
                        this.ReceivedCallback.Invoke(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF });
                        return;
                    }

                    this.ReceivedCallback.Invoke(buffer);
```

- [ ] **Step 6b: Stop a stray header ack from killing the serial reader (F26)**

Write the failing test first. Add to `IcomTerminalEchoTests`:

```csharp
        [Fact]
        public void OutOfStateCallbacksDoNotThrow()
        {
            var serial = new FakeConnection();
            var echo = new IcomTerminalEcho(new IcomTerminalWriter(), serial);

            // Nothing has been recorded, so the queue is empty. The radio sends
            // 03 21 00 ff and 03 03 01 ff together, and after a completed playback
            // the echo is back in Receiving with an empty queue - so a late or
            // duplicated header ack lands here in normal operation.
            echo.OnHeaderAck(new IcomTerminalHeaderAck(CaptureBytes.Stripped(CaptureBytes.IcomHeaderAckWire)));
            echo.OnFrameAck(new IcomTerminalFrameAck(CaptureBytes.Stripped(CaptureBytes.IcomFrameAckWire)));
            echo.OnPong(new IcomTerminalPong(CaptureBytes.Stripped(CaptureBytes.IcomPongGoAheadWire)));

            Assert.Empty(serial.Sent);
        }
```

Run: `dotnet test --filter OutOfStateCallbacksDoNotThrow`
Expected: FAIL with `InvalidOperationException: Queue empty` from `EchoHeader`'s `Peek()`. That exception is thrown on the background reader thread in the real app, where it kills the reader loop silently.

Guard both echo helpers, since each one peeks:

```csharp
        private void EchoHeader()
        {
            if (transceiverQueue.Count == 0)
            {
                return;
            }

            var packet = transceiverQueue.Peek();
            ...
        }

        private void EchoFrame()
        {
            if (transceiverQueue.Count == 0)
            {
                return;
            }

            var packet = transceiverQueue.Peek();
            ...
        }
```

and guard the dequeue in `OnFrameAck` the same way:

```csharp
            if (state == StateType.TransmittingFrames)
            {
                if (frameAckPacket.Ack && transceiverQueue.Count > 0)
                {
                    transceiverQueue.Dequeue();
                    Thread.Sleep(12);
                    EchoFrame();
                }
            }
```

Run: `dotnet test --filter IcomTerminalEchoTests`
Expected: PASS, including the three playback tests from Task 0.

- [ ] **Step 7: Run the whole suite and build**

Run: `dotnet build && dotnet test`
Expected: PASS

- [ ] **Step 8: Commit**

```bash
git add BlackEye.Connectivity/UdpConnection.cs BlackEye.Connectivity/SerialConnection.cs \
        BlackEye.Connectivity/IcomTerminal/IcomTerminalReader.cs BlackEye/IcomSerialEcho.cs \
        BlackEye.Tests/IcomTerminalReaderTests.cs BlackEye.Tests/IcomTerminalEchoTests.cs
git commit -m "fix(transport): survive malformed input and out-of-state callbacks"
```

---

## Open Questions

These are unresolved by the docs and the captures together. None blocks the plan; each is a judgement call recorded so the next reader knows it was made deliberately.

1. **Which filler payload is right — `16 29 F5` or `97 CB E5`?** `IcomTerminalMode.md` documents `97 CB E5` and `significant_bytes.txt` lists both (calling `16 29 F5` "unknown"), but `97 CB E5` occurs in zero bytes across all eight dumps while `16 29 F5` is what both reference applications send. Task 7 follows the captures. If the radio rejects it on hardware, revisit — and update `../dstardocs/IcomTerminalMode.md` either way.
2. **Is the sync frame really sent at number 0?** `significant_bytes.txt` says the first packet after a header is the empty-voice/data-sync frame, and the radio does send `55 2D 16` at number 0 in its own direction. But in the computer→radio direction neither reference app ever sends `55 2D 16` — both send `16 29 F5` at number 0. Task 9 keeps the `number == 0 → sync` rule because it matches the D-STAR 21-frame superframe structure; the conflicting evidence is recorded here.
3. **Slow-data sync alignment when relaying.** Radio-bound frames get our own number, but their 3 slow-data bytes come from the network stream with that stream's own sync phase baked in. If the two phases disagree the radio may see a sync pattern at the wrong frame index. Not addressed by this plan; needs a hardware observation to even confirm it matters.
4. **DPlus header CRC byte order.** Task 5 writes low byte first, matching the radio's own header, and the algorithm itself is now confirmed: CRC-X25 over the 39 byte RF header reproduces the ID52's `58 14` exactly. The byte order on the DPlus side remains unverifiable from the captures, because the reference client emits a constant `00 0b` rather than a real CRC (the true CRC of that header is `e3 94`), and xlxd stores and echoes the field without ever checking it.
5. **Should the operator's urcall pass through?** Task 10 forwards `headerPacket.UrCall` unchanged so that routed calls still work, while replacing rpt1/rpt2/mycall. If the reflector rejects non-`CQCQCQ` urcalls, force `config.UrCall` instead.
6. **`DPlusHandler` is still not reachable from `Main`.** `Program.cs` wires `IcomTerminalEcho`. Switching the entry point is deliberately out of scope — it needs the connect/login path from Task 10 to be exercised against a live reflector first.

---

## Verification Summary

| Level | Command | Covers |
|---|---|---|
| Build | `dotnet build` — must stay at 0 warnings | All tasks |
| Unit | `dotnet test` — 96 passing as of Task 0, and the suite must be green at the end of every task | Tasks 1-11 |
| Coverage | `dotnet test --collect:"XPlat Code Coverage" --results-directory TestResults` | Line coverage was 54% after Task 0, with the protocol layer at 83-100% per class and `DPlusHandler` at 0%. Tasks 8-10 are what move that number; expect ~75%+ overall once they land. |
| Byte-level | Assertions against `BlackEye.Tests/CaptureBytes` | F01-F09, F12, F13, F15-F18, F21 |
| Hardware (Windows + ID52 on COM4) | `dotnet run --project BlackEye` | Task 7 and Task 11 regressions on the echo path; nothing else in this plan is reachable from `Main` |
| Live reflector | Not yet possible | F19, F20 — needs the entry point switched, which is out of scope |

`SerialConnection` and `Program` stay uncovered by design: one needs a serial port, the other is the entry point. `UdpConnection` becomes testable over loopback once Task 11 makes the client port injectable — worth a round-trip test at that point.
