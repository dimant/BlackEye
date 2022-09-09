# DCS Protocol

Used for: DCS, XLX, JPN reflectors. Goes over UDP, server port is 30051, client port is any.

## High Level

DCS packets don't really have a common structure. They do seem to have unique lengths which help in telling apart different types of packets.

1. Connect
2. Ack / Nak
3. Keep Alive
4. Frame
5. Frame End of Transmission
6. Go to 2.
7. Disconnect

## Packets
The protocol has the following types of packets:

- Connect
- Connect Ack/Nak
- Disconnect
- Keep Alive
- Keep Alive Response
- Ignore Packet
- Frame
- Frame End of Transmission

### Connect