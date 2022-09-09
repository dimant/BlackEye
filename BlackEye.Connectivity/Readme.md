# DPlus Protocol

Used for: REF, XRF reflectors

Takes place over UDP. The server side listens on 20001, the client sends from port 20002

Types of packets:

Connect, Disconnect, Login, KeepAlive, DvHeader, DvFrame, DvFrameEot

## Connect / Disconnect

Sent to initiate or terminate a connection. The server acknowledges the packet by echoing it back.

| 0    | 1    | 2    | 3    | 4    |
|------|------|------|------|------|
| 0x05 | 0x00 | 0x18 | 0x00 | 0x01 |

|Byte| description |
|---|---------------------------|
|0  | Length of frame (5 bytes) |
|4  | 1 indicates connect, 0 indicates disconnect |

The server replies with the same packet to ack:

    0x05 0x00 0x18 0x00 0x01

## Login

Sent to let the server verify we are allowed to connect with this callsign and ip address.
Response is LoginAck on successful connect, LoginNak if we got rejected.

    0    |  1   |   2  |    3
    0x1C | 0xC0 | 0x04 | 0x00

    4      5      6      7      8      9      10     11
    0x00 | 0x00 | 0x00 | 0x00 | 0x00 | 0x00 | 0x00 | 0x00

    12     13     14     15     16     17     18     19
    0x00 | 0x00 | 0x00 | 0x00 | 0x00 | 0x00 | 0x00 | 0x00

    20     21     22     23     24     25     26     27
    0x44 | 0x56 | 0x30 | 0x31 | 0x39 | 0x39 | 0x39 | 0x34

    Byte:
    0       Length of frame (28 bytes)
    1       Type is login
    4..11   mycall (8 bytes)
    12..19  0x00 - probably used when you link through a gw?
    20..27  'DV019994'

## LoginAck / Nak

    0      1      2      3      4     5     6     7
    0x08 | 0xC0 | 0x04 | 0x00 | 'O' | 'K' | 'R' | 'W' 

    0      1      2      3      4     5     6     7
    0x08 | 0xC0 | 0x04 | 0x00 | 'B' | 'U' | 'S' | 'Y' 

    Bytes:
    0       Length
    4..7    Response type

## KeepAlive
Sent periodically by the client so that the server keeps keeps us authorized. The
server replies by echoing the packet back.

    0      1      2
    0x03 | 0x60 | 0x00

## Dv Header to server
First package in a voice / data transmission. Contains routing information.
Server verifies mycall and rpt2 before the transmission goes on the air.
The header carries a randomly generated stream id which associates subsequent
header or voice frames with the same stream. Once the stream is closed, the
client will generate a new stream id. A second header for the same session
will be inored but the connection timeout timer will be reset.

    0      1      2     3     4     5     6      7      
    0x3A | 0x80 | 'D' | 'S' | 'V' | 'T' | 0x10 | 0x00

    8      9      10     11     12     13     14     15     16
    0x00 | 0x00 | 0x20 | 0x00 | 0x02 | 0x01 | 0x00 | 0x00 | 0x80

    [ some dstar routing stuff ]

    56     57
    0x00 | 0x0B

Example:

    0000  3a 80 44 53 56 54 10 00 00 00 20 00 02 01 7d 37   :.DSVT.... ...}7
    0010  80 00 00 00 52 45 46 30 33 30 20 43 41 49 36 56   ....REF030 CAI6V
    0020  57 20 20 44 43 51 43 51 43 51 20 20 41 49 36 56   W  DCQCQCQ  AI6V
    0030  57 20 20 20 49 44 35 32 00 0b                     W   ID52..

| Bytes | description |
|-------|-------------|
| 0     | Length (58 bytes) |
| 2     | 0x80 |
| 3..5  | 'D' 'S' 'V' 'T' |
| 6     | 0x10 |
| 7     | 0x00 |
| 8     | 0x00 |
| 9     | 0x00 |
| 10    | 0x20 |
| 11    | 0x00 |
| 12    | 0x02 |
| 13    | 0x01 |
| 14    | session id byte 1 |
| 15    | session id byte 2 |
| 16    | 0x80 dstar header type? |
| 17    | 0x00 |
| 18    | 0x00 |
| 19    | 0x00 |
| 20..27 | 'REF030 C' Rpt2     |
| 28..35 | 'AI6VW  D' Rpt1   |
| 36..43 | 'CQCQCQ  ' ur call|
| 44..51 | 'AI6VW   ' my call|
| 52..55 | 'ID52'     suffix |
| 56     | 0x00 crc byte 1 |
| 57     | 0x0b crc byte 2 |

## Dv Voice Frame to server

Example:

    0000  1d 80 44 53 56 54 20 00 00 00 20 00 02 01 7d 37   ..DSVT ... ...}7
    0010  01 5e a5 06 52 15 b0 46 20 b6 25 4f 93            .^..R..F .%O.

| Byte | Description |
|------|-------------|
| 0    | Len (29 bytes) |
| 1    | 0x80 |
| 2..5 | 'D' 'S' 'V' 'T' |
| 6    | 0x20 |
| 7    | 0x00 |
| 8    | 0x00 |
| 8    | 0x00 |
| 9    | 0x20 |
| 10   | 0x00 |
| 11   | 0x02 |
| 12   | 0x01 |
| 13   | session id byte 1 |
| 14   | session id byte 2 |
| 15   | packet id |
| 16..18 | 12 bytes = 9 bytes voice + 3 bytes data |

## Dv Voice End Frame to server

Example:

    0000  20 80 44 53 56 54 20 00 00 00 20 00 02 01 7d 37    .DSVT ... ...}7
    0010  52 9e 8d 32 88 26 1a 3f 61 e8 55 55 55 55 c8 7a   R..2.&.?a.UUUU.z

| Byte | Description |
|------|-------------|
| 0    | Len (32 bytes) |
| 1    | 0x80 |
| 2..5 | 'D' 'S' 'V' 'T' |
| 6    | 0x20 |
| 7    | 0x00 |
| 8    | 0x00 |
| 9    | 0x00 |
| 10    | 0x20 |
| 11   | 0x00 |
| 12   | 0x02 |
| 13   | 0x01 |
| 14   | session id byte 1 |
| 15   | session id byte 2 |
| 16   | packet id |
| 17..28  | 12 bytes = 9 bytes voice + 3 bytes data  |
| 29 | ?? |
| 30 | ?? |
| 31 | ?? |

# DCS Protocol

Used for: DCS, XLX, JPN reflectors

Server port is 30051, client port is 30052

## Login

Example:

    0020                                41 49 36 56 57 20             AI6VW  
    0030  20 20 44 41 00 44 43 53 38 30 31 20 20 3c 74 61     DA.DCS801  <ta
    0040  62 6c 65 20 62 6f 72 64 65 72 3d 22 30 22 20 77   ble border="0" w
    0050  69 64 74 68 3d 22 39 35 25 22 3e 3c 74 72 3e 3c   idth="95%"><tr><
    0060  74 64 20 77 69 64 74 68 3d 22 34 25 22 3e 3c 61   td width="4%"><a
    0070  20 68 72 65 66 3d 22 68 74 74 70 3a 2f 2f 77 77    href="http://ww
    0080  77 2e 70 61 37 6c 69 6d 2e 6e 6c 2f 22 3e 3c 69   w.pa7lim.nl/"><i
    0090  6d 67 20 62 6f 72 64 65 72 3d 22 30 22 20 73 72   mg border="0" sr
    00a0  63 3d 22 68 74 74 70 3a 2f 2f 64 63 73 30 30 31   c="http://dcs001
    00b0  2e 78 72 65 66 6c 65 63 74 6f 72 2e 6e 65 74 2f   .xreflector.net/
    00c0  68 6f 74 73 70 6f 74 2e 6a 70 67 22 3e 3c 2f 74   hotspot.jpg"></t
    00d0  64 3e 3c 74 64 20 77 69 64 74 68 3d 22 39 36 25   d><td width="96%
    00e0  22 3e 3c 66 6f 6e 74 20 73 69 7a 65 3d 22 32 22   "><font size="2"
    00f0  3e 3c 62 3e 44 6f 6f 7a 79 3c 2f 62 3e 20 62 79   ><b>Doozy</b> by
    0100  20 50 41 37 4c 49 4d 20 72 75 6e 6e 69 6e 67 20    PA7LIM running 
    0110  6f 6e 20 57 69 6e 64 6f 77 73 20 76 65 72 73 69   on Windows versi
    0120  6f 6e 20 31 2e 30 2e 30 2e 31 36 3c 2f 66 6f 6e   on 1.0.0.16</fon
    0130  74 3e 3c 2f 74 64 3e 3c 2f 74 72 3e 3c 2f 74 61   t></td></tr></ta
    0140  62 6c 65 3e 20 20 20 20 20 20 20 20 20 20 20 20   ble>            
    0150  20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20                   
    0160  20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20                   
    0170  20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20                   
    0180  20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20                   
    0190  20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20                   
    01a0  20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20                   
    01b0  20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20                   
    01c0  20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20                   
    01d0  20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20                   
    01e0  20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20                   
    01f0  20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20                   
    0200  20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20                   
    0210  20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20                   
    0220  20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20                   
    0230  20  

Length: 519 bytes

| Bytes | Description |
|-------|-------------|
| 0..7  | my call     |
| 8     | my module   |
| 9     | ur module   |
| 17    | 0x00        |
| 18..25| ur call     |
| 26..  | some html padded with 0x20 |

## Login Ack

The server replies to a login request with either ACK or NAK.

Example:

    0020                                41 49 36 56 57 20             AI6VW 
    0030  20 20 44 41 41 43 4b 00                             DAACK.

Length 14 bytes

| Bytes | Description |
|-------|-------------|
| 0..6  | my call     |
| 7     | 0x20 ' '    |
| 8     | my module   |
| 9     | ur module   |
| 10..12| ACK / NAK   |
| 13    | 0x00        |

## Keep Alive

Sent periodically by the client to see if the server is still around. There are 2 versions of the ping packet, 1 with 9 bytes length and 1 with 22 bytes length. It's ok to ignore the 9 byte ping and only reply to the 22 byte ping.

Example:

    0020                                44 43 53 38 30 31             DCS801
    0030  20 41 20 41 49 36 56 57 20 20 44 44 0a 00 20 20    A AI6VW  DD..  

Length 22 bytes

| Bytes | Description |
|-------|-------------|
| 0..7  | urcall 'DCS801 A'   |
| 8     | ' '                 |
| 9..16 | mycall 'AI6VW  D'   |
| 17    | mymodule 'D'        |
| 18..21| 0x0a 0x00 0x20 0x20 |

The server then responds with a pong.

Example:

    0020                                41 49 36 56 57 20             AI6VW 
    0030  20 44 00 44 43 53 38 30 31 20 41                   D.DCS801 A

Length 17 bytes

| Bytes | Description |
|-------|-------------|
| 0..7  | mycall 'AI6VW  D' |
| 8     | 0x00              |
| 9..16 | urcall 'DCS801 A' |

## Disconnect

Example:

    0020                                41 49 36 56 57 20             AI6VW 
    0030  20 20 44 20 00 44 43 53 38 30 31 20 20              D .DCS801

Length 19 bytes

| Bytes  | Description |
|--------|-------------|
| 0..6   | mycall      |
| 7      | ' '         |
| 8      | my module   |
| 9      | ' '         |
| 10     | 0x00        |
| 11..16 | urcall      |
| 17     | ' '         |
| 18     | 0x00        |

The disconnect is acknowledged with a NAK.

Example:

    0020                             41 49 36 56 57 20             AI6VW 
    0030  20 20 44 20 4e 41 4b 00                             D NAK.

Length 14 bytes

| Bytes  | Description |
|--------|-------------|
| 0..6   | mycall      |
| 7      | ' '         |
| 8      | my module   |
| 9      | ' '         |
| 10..12 | 'N' 'A' 'K' |
| 13     | 0x00        |


## Ignore Packet

Valid but ignore.

Length 15 bytes

| Bytes  | Description |
|--------|-------------|
| 0..15  | 0x00        |

## Dv Packet

In the DCS protocol, the dv header is sent with each packet. A packet will have: dvheader + dv voice or dv header + dv last frame.

Example:

    0020                                30 30 30 31 00 00             0001..
    0030  00 44 43 53 38 30 31 20 41 41 49 36 56 57 20 20   .DCS801 AAI6VW  
    0040  44 43 51 43 51 43 51 20 20 41 49 36 56 57 20 20   DCQCQCQ  AI6VW  
    0050  20 49 44 35 32 39 30 0e 5f c2 8e 63 d7 13 a2 35    ID5290._..c...5
    0060  9a 50 6f b3 23 00 00 01 00 21 44 6f 6f 7a 79 20   .Po.#....!Doozy 
    0070  66 6f 72 20 57 69 6e 64 6f 77 73 20 20 20 00 00   for Windows   ..
    0080  00 00 00 00 00 00 00 00 00 00 00 00 00 00         ..............

Length 100 bytes.

| Bytes  | Description |
|--------|-------------|
| 0..3   | '0' '0' '0' '1'   |
| 4..6   | 0x00              |
| 7..14  | Rpt2   'DCS801 A' |
| 15..22 | Rpt1   'AI6VW  D' |
| 23..30 | urcall 'CQCQCQ  ' |
| 31..38 | mycall 'AI6VW   ' |
| 39..42 | suffix 'ID52'     |
| 43..44 | session id        |
| 45     | packet id         |
| 46..57 | 12 bytes = 9 bytes voice + 3 bytes data |
| 58..60 | 3 bytes dcs session counter least to most significant byte |
| 61     | 0x01              |
| 62     | 0x00              |
| 63..99 | 38 bytes 0x00     |

Alternatively, 63..69 may be used as 1 byte len, string with client info, pad with 0x00 (kind of) as in the example above.

## Dv Last Packet

The structure of the last packet is dictated by dstar. The DCS layer is unchanged.

Example:

    0020                                30 30 30 31 00 00             0001..
    0030  00 44 43 53 38 30 31 20 41 41 49 36 56 57 20 20   .DCS801 AAI6VW  
    0040  44 43 51 43 51 43 51 20 20 41 49 36 56 57 20 20   DCQCQCQ  AI6VW  
    0050  20 49 44 35 32 39 30 40 55 55 55 55 c8 7a 00 00    ID5290@UUUU.z..
    0060  00 00 00 00 40 00 00 01 00 21 44 6f 6f 7a 79 20   ....@....!Doozy 
    0070  66 6f 72 20 57 69 6e 64 6f 77 73 20 20 20 00 00   for Windows   ..
    0080  00 00 00 00 00 00 00 00 00 00 00 00 00 00         ..............

| Bytes  | Description |
|--------|-------------|
| 0..3   | '0' '0' '0' '1'   |
| 4..6   | 0x00              |
| 7..14  | Rpt2   'DCS801 A' |
| 15..22 | Rpt1   'AI6VW  D' |
| 23..30 | urcall 'CQCQCQ  ' |
| 31..38 | mycall 'AI6VW   ' |
| 39..42 | suffix 'ID52'     |
| 43..44 | session id        |
| 45     | 0x40              |
| 46..49 | 0x55              |
| 50     | c8                |
| 51     | 7a                |
| 52..57 | 0x00              |
| 58..60 | 0x40 0x00 0x00    |
| 61     | 0x01              |
| 62     | 0x00              |
| 63..99 | 38 bytes 0x00     |


# Terminal Mode Serial Protocol

## Ping

```
02 02 ff
```
## Pong

```
03 03 00 ff
```

## Dv Header from terminal

to doozy
```
    2c 10 00 00 00 44 49 52 45 43 54 20 20 44 49 52   ,....DIRECT  DIR	
    45 43 54 20 20 43 51 43 51 43 51 20 20 41 49 36   ECT  CQCQCQ  AI6	
    56 57 20 20 20 49 44 35 32 58 14 00 ff
```
to rs-ms3w
```
    xx xx xx xx 2c 10 00 00 00 44 49 52 45 43 54 20       ,....DIRECT 	
    20 44 49 52 45 43 54 20 20 43 51 43 51 43 51 20    DIRECT  CQCQCQ 	
    20 41 49 36 56 57 20 20 20 49 44 35 32 58 14 00    AI6VW   ID52X..	
    ff                                                
```

| Bytes: | description |
|--------|-------------|
| 0      | Len (44 bytes) |
| 1      | 0x10 type header |
| 2      | 0x00 |
| 3      | 0x00 |
| 4      | 0x00 |
| 5..12  | Rpt1   ('DIRECT  ') |
| 13..20 | Rpt2 ('DIRECT  ') |
| 21..28 | ur  ('CQCQCQ  ')  |
| 29..36 | my  ('AI6VW   ')  |
| 37..40 | suffix |
| 41     | 0x14 |
| 42     | 0x00 |
| 43     | 0xff |

## Dv Frame from terminal

to doozy:
```
                                           ff 10 12               
    01 01 5e a5 06 52 15 b0 46 20 b6 25 4f 93 ff      
```

to rs-ms3w:
```
    ff 10 12 00 00 b2 4d 22 48 c0 16 28 26 c8 55 2d   
    16 ff                                             
```

    0       Len (16 bytes)
    1       0x12 type dv frame
    2       packet id?
    3       packet id?
    4..15   12 = 9 bytes ambe + 3 bytes data
    16      0xff


## Dv Header to terminal

from doozy
```
    [03/09/2022 08:26:36] Written data (COM4)	
        29 20 01 00 00 41 49 36 56 57 00 20 20 41 49 36   ) ...AI6VW.  AI6	
        56 57 00 20 20 41 49 36 56 57 00 20 20 41 49 36   VW.  AI6VW.  AI6	
        56 57 00 20 20 20 20 20 20 ff                     VW.           	
    [03/09/2022 08:26:36] Read data (COM4)	
        03 21 00 ff 03 03 01 ff                                	
```

from rs-ms3w
```
    [02/09/2022 22:38:47] Written data (COM4)	
        29 20 01 00 00 41 49 36 56 57 20 20 4c 41 49 36   ) ...AI6VW  LAI6	
        56 57 20 20 47 41 49 36 56 57 20 20 20 41 49 36   VW  GAI6VW   AI6	
        56 57 20 20 47 20 20 20 20 ff                     VW  G          	
    [02/09/2022 22:38:47] Read data (COM4)	
        03 21 00 ff 03 03 01 ff                             
```

| Bytes | description |
|-------|-------------|
| 0     | Len (41 bytes) |
| 1     | type header (0x20) |
| 2     | 0x01 |
| 3     | 0x00 |
| 4     | 0x00 |
| 5..12  | Rpt1 mycall L (8 bytes) |
| 13..20 | Rpt2 mycall G (8 bytes) |
| 21..28 | ur mycall (8 bytes) |
| 29..36 | my mycall G (8 bytes) |
| 37..40 | suffix (4 bytes) |

response:

    0x03 0x21 0x00 0xff     presumably: got the header
    0x03 0x03 0x01 0xff     presumably: start sending voice packets

## Dv Frame to Terminal

from rs-ms3w
```
    [02/09/2022 22:38:47] Written data (COM4)	
        10 22 00 00 9e 8d 32 88 26 1a 3f 61 e8 16 29 f5   	
        ff                                                               	
    [02/09/2022 22:38:47] Read data (COM4)	
        04 23 00 00 ff                                      
```

from doozy
```
    [03/09/2022 08:26:36] Written data (COM4)	
        10 22 00 00 9e 8d 32 88 26 1a 3f 61 e8 16 29 f5   	
        ff                                                
    [03/09/2022 08:26:36] Read data (COM4)	
        04 23 01 00 ff                                    
```

| Bytes | description |
|-------|-------------|
| 0     | Len (16 bytes) |
| 1     | 0x22 dv frame type |
| 2     | 0x00  packet id |
| 3     | 0x00  packet id |
| 4..15 | 12 bytes (9 bytes voice + 3 bytes data) |
| 16    | 0xff |


response:

    0x04 0x23 0x00 0x00 0xff    presumably: ack packet 0x00 0x00
    0x04 0x23 0x00 0x01 0xff    presumably: ack packet 0x01 0x01

## Dv Frame end of voice transmission to terminal

from rs-ms3w
```
    [02/09/2022 22:38:47] Written data (COM4)	
        10 22 08 48 55 c8 7a 55 55 55 55 55 55 55 55 55   	
        ff                                                               	
    [02/09/2022 22:38:47] Read data (COM4)	
        04 23 08 00 ff 03 03 00 ff                          
```

from doozy
```
    [03/09/2022 08:26:36] Written data (COM4)	
        10 22 08 48 55 c8 7a 55 55 55 55 55 55 55 55 55   	
        ff                                                              	
    [03/09/2022 08:26:36] Read data (COM4)	
        04 23 08 00 ff 03 03 00 ff                        
```

| Bytes | description |
|-------|-------------|
| 0      | Length (16 bytes) |
| 1      | 0x22 (type end of voice transmission) |
| 2      | 0x08 |
| 3      | 0x48 |
| 4      | 0x55 |
| 5      | 0xc8 |
| 6      | 0x7a |
| 7..15  | 0x55 |
| 16     | 0xff |