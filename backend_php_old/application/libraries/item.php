<?php
defined('BASEPATH') OR exit('No direct script access allowed');

class item{
	public $item_id;
	public $name;
	public $description;
	public $last_market_price;
	public $shop_price;
	public $icon;
	public $market_updated_at;
	public $updated_at;


	/**
	 * Get the value of item_id
	 */ 
	public function getItem_id()
	{
		return $this->item_id;
	}

	/**
	 * Set the value of item_id
	 *
	 * @return  self
	 */ 
	public function setItem_id($item_id)
	{
		$this->item_id = $item_id;

		return $this;
	}

	/**
	 * Get the value of name
	 */ 
	public function getName()
	{
		return $this->name;
	}

	/**
	 * Set the value of name
	 *
	 * @return  self
	 */ 
	public function setName($name)
	{
		$this->name = $name;

		return $this;
	}

	/**
	 * Get the value of description
	 */ 
	public function getDescription()
	{
		return $this->description;
	}

	/**
	 * Set the value of description
	 *
	 * @return  self
	 */ 
	public function setDescription($description)
	{
		$this->description = $description;

		return $this;
	}

	/**
	 * Get the value of last_market_price
	 */ 
	public function getLast_market_price()
	{
		return $this->last_market_price;
	}

	/**
	 * Set the value of last_market_price
	 *
	 * @return  self
	 */ 
	public function setLast_market_price($last_market_price)
	{
		$this->last_market_price = $last_market_price;

		return $this;
	}

	/**
	 * Get the value of shop_price
	 */ 
	public function getShop_price()
	{
		return $this->shop_price;
	}

	/**
	 * Set the value of shop_price
	 *
	 * @return  self
	 */ 
	public function setShop_price($shop_price)
	{
		$this->shop_price = $shop_price;

		return $this;
	}

	/**
	 * Get the value of icon
	 */ 
	public function getIcon()
	{
		return $this->icon;
	}

	/**
	 * Set the value of icon
	 *
	 * @return  self
	 */ 
	public function setIcon($icon)
	{
		$this->icon = $icon;

		return $this;
	}
}
