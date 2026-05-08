"""One-shot loaders for world / item reference data from Scylla.

Both server/ and sales_importer/ used to query these tables independently
(sometimes twice in a single module). We cache the result for the lifetime
of the process.
"""
from __future__ import annotations

from functools import lru_cache

from . import db


@lru_cache(maxsize=1)
def world_dict() -> dict[int, dict]:
    rows = db.get_session().execute("SELECT id, name, datacenter, region FROM worlds")
    return {
        r.id: {"id": r.id, "name": r.name, "datacenter": r.datacenter, "region": r.region}
        for r in rows
    }


@lru_cache(maxsize=1)
def item_name_dict() -> dict[int, str]:
    rows = db.get_session().execute("SELECT id, name FROM items")
    return {r.id: r.name for r in rows}


@lru_cache(maxsize=1)
def marketable_item_name_dict() -> dict[int, str]:
    rows = db.get_session().execute("SELECT id, name FROM items WHERE marketable = true")
    return {r.id: r.name for r in rows}


@lru_cache(maxsize=1)
def marketable_item_id_list() -> list[int]:
    return sorted(marketable_item_name_dict().keys(), reverse=True)


@lru_cache(maxsize=1)
def region_list() -> list[str]:
    return sorted({w["region"] for w in world_dict().values()})


def world_names_for_region(region: str) -> list[str]:
    return [w["name"] for w in world_dict().values() if w["region"] == region]
