import { useEffect, useRef, useState } from 'react';
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
  const [sales, setSales] = useState<EnrichedSale[]>([]);
  const [status, setStatus] = useState<StreamStatus>('connecting');
  const itemNameCache = useRef(new Map<number, string>());
  const backoffRef = useRef(1_000);
  const deadRef = useRef(false);
  const wsRef = useRef<WebSocket | null>(null);

  useEffect(() => {
    if (!worlds.data) return;

    const worldMap = buildWorldMap(worlds.data);
    const worldIds = Array.from(worldMap.keys());
    deadRef.current = false;
    backoffRef.current = 1_000;

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

      const ws = new WebSocket(WS_URL, ['json']);
      wsRef.current = ws;

      ws.onopen = () => {
        backoffRef.current = 1_000;
        for (const id of worldIds) {
          ws.send(JSON.stringify({ event: 'subscribe', channel: `sales/add{world=${id}}` }));
        }
        setStatus('connected');
      };

      ws.onmessage = async (evt: MessageEvent) => {
        let data: unknown;
        try {
          const text = typeof evt.data === 'string' ? evt.data : await (evt.data as Blob).text();
          data = JSON.parse(text);
        } catch {
          return;
        }

        if (!isRecord(data) || data['event'] !== 'sales/add') return;
        const worldId = Number(data['world']);
        const itemId = Number(data['item']);
        const rawSales = data['sales'];
        if (!Array.isArray(rawSales) || !worldId || !itemId) return;

        const worldName = worldMap.get(worldId) ?? String(worldId);
        const itemName = await resolveItemName(itemId);

        if (deadRef.current) return;

        const newEntries: EnrichedSale[] = rawSales
          .filter(isRecord)
          .filter((s) => typeof s['buyerName'] === 'string' && s['buyerName'])
          .map((s) => ({
            key: `${itemId}-${worldId}-${s['timestamp']}`,
            itemId,
            itemName,
            worldName,
            buyerName: s['buyerName'] as string,
            hq: s['hq'] === true,
            quantity: Number(s['quantity']) || 1,
            unitPrice: Number(s['pricePerUnit']) || 0,
            saleTime: Number(s['timestamp']),
          }));

        if (newEntries.length === 0) return;

        setSales((prev) => [...newEntries, ...prev].slice(0, BUFFER_SIZE));
      };

      ws.onclose = () => {
        if (deadRef.current) return;
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
      wsRef.current?.close();
    };
  }, [worlds.data]);

  return { sales, status };
}
