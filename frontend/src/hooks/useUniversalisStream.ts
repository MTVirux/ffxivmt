import { useCallback, useEffect, useRef, useState } from 'react';
import { deserialize, serialize } from 'bson';
import { useWorlds } from './useWorlds';
import { apiGet } from '../api/client';
import type { Item, WorldStructure } from '../api/types';

export type StreamStatus = 'connecting' | 'connected' | 'reconnecting';

export type EnrichedSale = {
  key: string;
  itemId: number;
  itemName: string;
  worldName: string;
  buyerName: string;
  hq: boolean;
  quantity: number;
  unitPrice: number;
  saleTime: number; // unix seconds
};

const WS_URL = 'wss://universalis.app/api/ws';
const BUFFER_SIZE = 50;
const MAX_BACKOFF_MS = 60_000;
const EXPIRY_S = 600;
const PRUNE_INTERVAL_MS = 10_000;

let salesCache: EnrichedSale[] = [];
let statusCache: StreamStatus = 'connecting';

function buildWorldMap(worlds: WorldStructure): Map<number, string> {
  const map = new Map<number, string>();
  for (const dcs of Object.values(worlds)) {
    for (const ws of Object.values(dcs)) {
      for (const [id, name] of Object.entries(ws)) {
        map.set(Number(id), name);
      }
    }
  }
  return map;
}

function isRecord(val: unknown): val is Record<string, unknown> {
  return typeof val === 'object' && val !== null && !Array.isArray(val);
}

export function useUniversalisStream() {
  const worlds = useWorlds();
  const worldsKey = worlds.data
    ? Object.values(worlds.data)
        .flatMap((dcs) => Object.values(dcs).flatMap((ws) => Object.keys(ws)))
        .sort()
        .join(',')
    : null;
  const [sales, setSalesState] = useState<EnrichedSale[]>(() => {
    const cutoff = Date.now() / 1000 - EXPIRY_S;
    return salesCache.filter((s) => s.saleTime > cutoff);
  });
  const [status, setStatusState] = useState<StreamStatus>(() => statusCache);

  const setSales = useCallback((updater: (prev: EnrichedSale[]) => EnrichedSale[]) => {
    setSalesState((prev) => {
      const next = updater(prev);
      salesCache = next;
      return next;
    });
  }, []);

  const setStatus = useCallback((s: StreamStatus) => {
    statusCache = s;
    setStatusState(s);
  }, []);
  const itemNameCache = useRef(new Map<number, string>());
  const backoffRef = useRef(1_000);
  const deadRef = useRef(false);
  const wsRef = useRef<WebSocket | null>(null);
  const generationRef = useRef(0);

  useEffect(() => {
    if (!worlds.data) return;

    const worldMap = buildWorldMap(worlds.data);
    const worldIds = Array.from(worldMap.keys());
    deadRef.current = false;
    backoffRef.current = 1_000;
    const generation = ++generationRef.current;

    async function resolveItemName(itemId: number): Promise<string> {
      const cached = itemNameCache.current.get(itemId);
      if (cached !== undefined) return cached;
      try {
        const item = await apiGet<Item>(`/item/${itemId}`);
        itemNameCache.current.set(itemId, item.name);
        return item.name;
      } catch {
        const fallback = String(itemId);
        itemNameCache.current.set(itemId, fallback);
        return fallback;
      }
    }

    function connect() {
      if (deadRef.current) return;

      const ws = new WebSocket(WS_URL);
      wsRef.current = ws;
      ws.binaryType = 'arraybuffer';
      let connectionDead = false;

      ws.onopen = () => {
        backoffRef.current = 1_000;
        for (const id of worldIds) {
          ws.send(serialize({ event: 'subscribe', channel: `sales/add{world=${id}}` }));
        }
        setStatus('connected');
      };

      ws.onmessage = (evt: MessageEvent) => {
        if (connectionDead) return;

        let data: unknown;
        try {
          data = deserialize(new Uint8Array(evt.data as ArrayBuffer));
        } catch {
          return;
        }

        if (!isRecord(data) || data['event'] !== 'sales/add') return;
        const worldId = Number(data['world']);
        const itemId = Number(data['item']);
        const rawSales = data['sales'];
        if (!Array.isArray(rawSales) || !worldId || !itemId) return;

        const worldName = worldMap.get(worldId) ?? String(worldId);
        const cachedName = itemNameCache.current.get(itemId);

        const newEntries: EnrichedSale[] = rawSales
          .filter(isRecord)
          .filter((s) => typeof s['buyerName'] === 'string' && s['buyerName'])
          .map((s, i) => ({
            key: `${itemId}-${worldId}-${s['timestamp']}-${i}`,
            itemId,
            itemName: cachedName ?? String(itemId),
            worldName,
            buyerName: s['buyerName'] as string,
            hq: s['hq'] === true,
            quantity: Number(s['quantity']) || 1,
            unitPrice: Number(s['pricePerUnit']) || 0,
            saleTime: Number(s['timestamp']) || 0,
          }));

        if (newEntries.length === 0) return;

        setSales((prev) => {
          const existing = new Set(prev.map((s) => s.key));
          const fresh = newEntries.filter((e) => !existing.has(e.key));
          if (fresh.length === 0) return prev;
          return [...fresh, ...prev].slice(0, BUFFER_SIZE);
        });

        if (cachedName === undefined) {
          void resolveItemName(itemId).then((name) => {
            if (connectionDead || name === String(itemId)) return;
            setSales((prev) => {
              const placeholder = String(itemId);
              if (!prev.some((s) => s.itemId === itemId && s.itemName === placeholder)) return prev;
              return prev.map((s) =>
                s.itemId === itemId && s.itemName === placeholder ? { ...s, itemName: name } : s,
              );
            });
          });
        }
      };

      ws.onclose = () => {
        connectionDead = true;
        if (deadRef.current || generationRef.current !== generation) return;
        setStatus('reconnecting');
        const delay = backoffRef.current;
        backoffRef.current = Math.min(backoffRef.current * 2, MAX_BACKOFF_MS);
        setTimeout(connect, delay);
      };

      ws.onerror = () => {
        ws.close();
      };
    }

    connect();

    return () => {
      deadRef.current = true;
      statusCache = 'reconnecting';
      wsRef.current?.close();
    };
  }, [worldsKey]);

  useEffect(() => {
    const id = setInterval(() => {
      const cutoff = Date.now() / 1000 - EXPIRY_S;
      setSales((prev) => {
        const next = prev.filter((s) => s.saleTime > cutoff);
        return next.length === prev.length ? prev : next;
      });
    }, PRUNE_INTERVAL_MS);
    return () => clearInterval(id);
  }, []);

  return { sales, status };
}
