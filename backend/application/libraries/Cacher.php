<?php

// https://forum.codeigniter.com/thread-62217.html

class Cacher {
  protected $CI;

  public function __construct()
  {
    $this->CI =& get_instance(); //grab an instance of CI
    $this->initiate_cache();
  }

  public function initiate_cache()
  {
    $this->CI->load->driver('cache', array('adapter' => 'file'));
  }
}


?>