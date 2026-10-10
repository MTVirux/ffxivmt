import { useCallback, useEffect, useRef, useState } from 'react';
import { useQueryClient, type QueryClient } from '@tanstack/react-query';
import { deserialize, serialize } from 'bson';
import { appConfigQuery } from './useAppConfig';
import { itemNamesQuery } from './useItemNames';
import { useWorlds } from './useWorlds';
import { apiGet } from '../api/client';
import { buildWorldNameMap } from '../lib/worlds';
import type { Item } from '../api/types';

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
  // sales/add carries every sale Universalis hadn't seen yet, often hours old, so expiry keys on arrival
  receivedAt: number; // unix seconds
};

const WS_URL = 'wss://universalis.app/api/ws';
const BUFFER_SIZE = 50;
const MAX_BACKOFF_MS = 60_000;
const EXPIRY_S = 600;
const PRUNE_INTERVAL_MS = 10_000;

let salesCache: EnrichedSale[] = [];
let statusCache: StreamStatus = 'connecting';

function isRecord(val: unknown): val is Record<string, unknown> {
  return typeof val === 'object' && val !== null && !Array.isArray(val);
}

export function dropExpired(sales: EnrichedSale[], now: number): EnrichedSale[] {
  const cutoff = now - EXPIRY_S;
  return sales.filter((s) => s.receivedAt > cutoff);
}

export function cachedItemName(queryClient: QueryClient, itemId: number): string | undefined {
  const version = queryClient.getQueryData(appConfigQuery.queryKey)?.item_names_version;
  const names = queryClient.getQueryData(itemNamesQuery(version).queryKey);
  return names?.[itemId] ?? queryClient.getQueryData<Item>(['item', itemId])?.name;
}

async function listedItemName(queryClient: QueryClient, itemId: number) {
  try {
    const { item_names_version: version } = await queryClient.fetchQuery(appConfigQuery);
    if (version === undefined) return undefined;
    const names = await queryClient.fetchQuery(itemNamesQuery(version));
    return names[itemId];
  } catch {
    return undefined;
  }
}

export async function resolveItemName(queryClient: QueryClient, itemId: number): Promise<string> {
  const listed = await listedItemName(queryClient, itemId);
  if (listed !== undefined) return listed;
  try {
    const item = await queryClient.fetchQuery({
      queryKey: ['item', itemId] as const,
      queryFn: () => apiGet<Item>(`/item/${itemId}`),
      staleTime: Infinity,
      gcTime: Infinity,
      retry: false,
    });
    return item.name;
  } catch {
    return String(itemId);
  }
}

export function useUniversalisStream() {
  const worlds = useWorlds();
  const queryClient = useQueryClient();
  const [sales, setSalesState] = useState<EnrichedSale[]>(() =>
    dropExpired(salesCache, Date.now() / 1000),
  );
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
  const backoffRef = useRef(1_000);
  const deadRef = useRef(false);
  const wsRef = useRef<WebSocket | null>(null);
  const generationRef = useRef(0);

  useEffect(() => {
    if (!worlds.data) return;

    const worldMap = buildWorldNameMap(worlds.data);
    const worldIds = Array.from(worldMap.keys());
    deadRef.current = false;
    backoffRef.current = 1_000;
    const generation = ++generationRef.current;

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
        const cachedName = cachedItemName(queryClient, itemId);
        const receivedAt = Date.now() / 1000;

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
            receivedAt,
          }));

        if (newEntries.length === 0) return;

        setSales((prev) => {
          const existing = new Set(prev.map((s) => s.key));
          const fresh = newEntries.filter((e) => !existing.has(e.key));
          if (fresh.length === 0) return prev;
          return [...fresh, ...prev].slice(0, BUFFER_SIZE);
        });

        if (cachedName === undefined) {
          void resolveItemName(queryClient, itemId).then((name) => {
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
  }, [worlds.data, queryClient, setSales, setStatus]);

  useEffect(() => {
    const id = setInterval(() => {
      setSales((prev) => {
        const next = dropExpired(prev, Date.now() / 1000);
        return next.length === prev.length ? prev : next;
      });
    }, PRUNE_INTERVAL_MS);
    return () => clearInterval(id);
  }, [setSales]);

  return { sales, status };
}
