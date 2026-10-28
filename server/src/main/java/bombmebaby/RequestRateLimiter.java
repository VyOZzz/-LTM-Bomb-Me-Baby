package bombmebaby;

/** A bounded sliding window, owned exclusively by one connection's reader thread. */
final class RequestRateLimiter {
    static final int MAX_REQUESTS = 120;
    static final long WINDOW_NS = 1_000_000_000L;
    private final long[] receivedAt = new long[MAX_REQUESTS];
    private int oldest;
    private int size;

    boolean allow(long nowNs) {
        while (size > 0 && nowNs - receivedAt[oldest] >= WINDOW_NS) {
            oldest = (oldest + 1) % MAX_REQUESTS;
            size--;
        }
        if (size == MAX_REQUESTS) return false;
        receivedAt[(oldest + size) % MAX_REQUESTS] = nowNs;
        size++;
        return true;
    }
}
