package bombmebaby;

import static org.junit.jupiter.api.Assertions.*;
import java.io.StringReader;
import java.io.IOException;
import java.util.List;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;
import org.junit.jupiter.api.Test;

class CompletionRegressionTest {
    @Test void exportRealServerSnapshotsForUnityValidation() throws Exception {
        var packets = new java.util.ArrayList<String>();
        for (GameMap map : GameMap.all()) {
            GameRoom room = playing(map);
            PlayerState local = room.players.get(0);
            packets.add(BombServer.JSON.toJson(GameRoom.data("type", "WELCOME", "playerId", local.id, "hasRoom", false)));
            packets.add(BombServer.JSON.toJson(GameRoom.data("type", "ROOM_STATE", "room", room.roomInfo())));
            packets.add(BombServer.JSON.toJson(room.snapshot(1000, local, true)));
            room.players.get(1).alive = false;
            room.update(1000, 0);
            packets.add(BombServer.JSON.toJson(room.roundResult()));
            packets.add(BombServer.JSON.toJson(GameRoom.data("type", "ROOM_STATE", "room", room.roomInfo())));
            packets.add(BombServer.JSON.toJson(GameRoom.data("type", "LEFT_ROOM")));
        }
        java.nio.file.Files.write(java.nio.file.Path.of("target/unity-fixtures.jsonl"), packets);
        assertEquals(18, packets.size());
    }

    GameRoom playing(GameMap map) {
        GameRoom room = new GameRoom("REG001", SyncMode.active());
        room.map = map; room.obstacles = map.obstacles; room.bushes = map.bushes;
        room.random.setSeed(42);
        room.join(new PlayerState("A", null)); room.join(new PlayerState("B", null));
        for (PlayerState p : room.players) room.ready(p, true);
        room.orbs.clear();
        return room;
    }

    void assertFree(GameRoom room, PlayerState p) {
        double radius = p.size * GameRules.PLAYER_RADIUS;
        assertTrue(Math.abs(p.x) + radius <= room.map.halfSize + 1e-8);
        assertTrue(Math.abs(p.z) + radius <= room.map.halfSize + 1e-8);
        for (var rock : room.obstacles)
            assertTrue(Math.hypot(p.x - rock.x(), p.z - rock.z()) + 1e-8 >= radius + rock.radius(), "Overlap with " + rock);
    }

    @Test void growthBesideEveryRockOnEveryMapDoesNotTrapPlayer() {
        for (GameMap map : GameMap.all()) {
            GameRoom room = playing(map);
            PlayerState p = room.players.get(0);
            for (var rock : room.obstacles) {
                p.size = p.baseSize = 1; p.dx = p.dz = 0;
                p.x = rock.x() + rock.radius() + .401; p.z = rock.z();
                room.orbs.clear();
                for (int i = 0; i < 8; i++) room.orbs.add(new GameRoom.Pickup(1000 + i, p.x, p.z));
                room.update(1000, 0);
                assertEquals(1.96, p.size, .0001); assertFree(room, p);
                double beforeX = p.x, beforeZ = p.z;
                for (int i = 1; i <= 20; i++) {
                    room.action(p, "MOVE", p.x - rock.x(), p.z - rock.z(), 1000 + i * 50);
                    room.update(1000 + i * 50, .05);
                }
                assertTrue(Math.hypot(p.x - beforeX, p.z - beforeZ) > .05);
                assertFree(room, p);
            }
        }
    }

    @Test void growthAtBoundaryAndConcaveRockClusterFindsFreeSpace() {
        GameRoom room = playing(GameMap.all().get(0));
        PlayerState p = room.players.get(0);
        for (boolean boundary : List.of(true, false)) {
            p.size = p.baseSize = 1;
            p.x = boundary ? room.map.halfSize - .401 : 0; p.z = 0;
            room.obstacles = boundary ? List.of() : List.of(new GameMap.Obstacle(-1, 0, .59), new GameMap.Obstacle(1, 0, .59));
            room.orbs.clear();
            for (int i = 0; i < 8; i++) room.orbs.add(new GameRoom.Pickup(1000 + i, p.x, p.z));
            room.update(1000, 0); assertFree(room, p);
        }
    }

    @Test void coreGrowthAlsoResolvesCollision() {
        GameRoom room = playing(GameMap.all().get(0));
        PlayerState p = room.players.get(0);
        var rock = room.obstacles.get(0);
        p.x = rock.x() + rock.radius() + .401; p.z = rock.z();
        room.cores.add(new GameRoom.Pickup(9000, p.x, p.z));
        room.update(1000, 0);
        assertEquals(1.1, p.baseSize, .0001); assertFree(room, p);
    }

    @Test void boostOnlyDropsEnergyActuallySpent() {
        GameRoom room = playing(GameMap.all().get(0));
        PlayerState p = room.players.get(0);
        for (double size : new double[] {1, 1.05, 1.12, 1.5}) {
            p.x = p.z = p.dx = p.dz = 0; p.size = size; p.hp = 70; p.boostReadyAt = 0;
            room.orbs.clear(); room.action(p, "USE_BOOST", 0, 0, 1000);
            assertEquals(size >= 1.12 ? 1 : 0, room.orbs.size());
            assertTrue(p.boostUntil > 1000, "Base-size boost still works");
            if (size == 1) { room.update(1000, 0); assertEquals(70, p.hp); assertEquals(1, p.size); }
        }
    }

    @Test void publishedHostRoomCannotBeRemovedByConcurrentCleanup() throws Exception {
        BombServer server = new BombServer();
        var workers = Executors.newFixedThreadPool(2);
        CountDownLatch start = new CountDownLatch(1);
        try {
            var create = workers.submit(() -> {
                start.await();
                for (int i = 0; i < 100; i++) server.createRoom(GameMode.QUICK, new PlayerState("Host", null));
                return null;
            });
            var cleanup = workers.submit(() -> {
                start.await();
                for (int i = 0; i < 1000; i++) server.rooms.values().forEach(server::removeIfEmpty);
                return null;
            });
            start.countDown(); create.get(5, TimeUnit.SECONDS); cleanup.get(5, TimeUnit.SECONDS);
            assertEquals(100, server.rooms.size());
            assertTrue(server.rooms.values().stream().allMatch(r -> r.players.size() == 1));
        } finally { workers.shutdownNow(); server.clients.shutdownNow(); server.gameLoop.shutdownNow(); }
    }

    @Test void boundedReaderAcceptsBoundaryAndRejectsExcessWithoutNewline() throws Exception {
        assertEquals("x".repeat(2048), BombServer.readFrame(new StringReader("x".repeat(2048) + "\n")));
        assertThrows(IOException.class, () -> BombServer.readFrame(new StringReader("x".repeat(2049))));
        assertNull(BombServer.readFrame(new StringReader("")));
    }

    @Test void snapshotIdentifiesRoomAndSuccessiveRounds() {
        GameRoom room = playing(GameMap.all().get(0));
        assertEquals(room.code, room.snapshot(1000).get("roomCode"));
        assertEquals(1, room.snapshot(1000).get("roundIndex"));
        room.players.get(1).alive = false; room.update(1000, 0);
        assertEquals(room.players.get(0).id, room.roundResult().get("winnerId"));
        for (PlayerState p : room.players) room.ready(p, true);
        assertEquals(2, room.snapshot(1000).get("roundIndex"));
        assertEquals("", room.roomInfo().get("roundWinnerId"));
    }
}
