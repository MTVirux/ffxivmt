<?php
  ('BASEPATH') OR exit('No direct script access allowed');

function get_navbar($navbar_structure){
    $navbar = $navbar_structure;

    $navbar_html = '<nav class="navbar navbar-expand-lg navbar-dark bg-primary" style="padding-left:20px;padding-right:20px;">
    <div class="container-fluid">
    <a class="navbar-brand" href="'.base_url("").'" style="margin-right:50px;">FINAL FANTASY XIV MARKET TOOLS</a>
    <button class="navbar-toggler" type="button" data-bs-toggle="collapse" data-bs-target="#main_nav"  aria-expanded="false" aria-label="Toggle navigation">
    <span class="navbar-toggler-icon"></span>
    </button>
    <div class="collapse navbar-collapse" id="main_nav">
    <ul class="navbar-nav">';

      foreach($navbar as $key => $value){
        if(!key_exists("link", $value)){
          $navbar_html .= '<li class="nav-item dropdown" id="'.str_replace(' ', '_', $key).'">'.
                          '<a class="nav-link dropdown-toggle" href="#" data-bs-toggle="dropdown">'.$key.'</a>'.
                          '<ul class="dropdown-menu">';
          $navbar_html .= get_navbar_handle_array($value);
          $navbar_html .= '</ul></li></a>';
        }else{
          $navbar_html .= '<li class="nav-item"> <a class="nav-link" href="'.base_url($value["link"]).'">'.$key.'</a> </li>';
        }
      }

    $navbar_html .= '</ul>';
    $navbar_html .= '</div>';
    $navbar_html .= '</div>';
    $navbar_html .= '</nav>';

    return $navbar_html;
}

function get_navbar_handle_array($input){

  $navbar_html ="";

  foreach($input as $key => $value){
    if(!key_exists("link", $value)){
      $navbar_html .= ' <li> <a class="dropdown-item" href="#">'.$key.' &raquo;</a>
                        <ul class="submenu dropdown-menu">';
                      
      $navbar_html .= get_navbar_handle_array($value);
      $navbar_html .= '</ul></li></a>';
    }else{
      $navbar_html .= '<li> <a class="dropdown-item" href="'.base_url($value["link"]).'">'.$key.'</a> </li>';
    }
  }

  return $navbar_html;
}


?>