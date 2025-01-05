<style>
    /* ============ desktop view ============ */
    @media all and (min-width: 992px) {
        .dropdown-menu li{ position: relative; 	}
        .nav-item .submenu{ 
            display: none;
            position: absolute;
            left:100%; top:-7px;
        }
        .nav-item .submenu-left{ 
            right:100%; left:auto;
        }
        .dropdown-menu > li:hover{ background-color: #f1f1f1 }
        .dropdown-menu > li:hover > .submenu{ display: block; }
    }	
    /* ============ desktop view .end// ============ */

    /* ============ small devices ============ */
    @media (max-width: 991px) {
    .dropdown-menu .dropdown-menu{
        margin-left:0.7rem; margin-right:0.7rem; margin-bottom: .5rem;
    }
    }	
    /* ============ small devices .end// ============ */
</style>
<?php
$navbar = $this->config->item('navbar_structure');
$github_link = $this->config->item('github_link');



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

#Add Github Link to the right of the navbar
$navbar_html .= '<ul class="navbar-nav github-nav">';
$navbar_html .= '<li class="nav-item"> <a class="nav-link" href="'.$github_link.'">';
$navbar_html .= '<img class="github-logo" src="'.base_url("resources/img/github_logo_25x25.png").'" alt="GitHub">';
$navbar_html .= '<span class="github-text">Project on Github</span>';
$navbar_html .= '</a>';
$navbar_html .= '</li>';
$navbar_html .= '</ul>';

#Close the navbar
$navbar_html .= '</div>';
$navbar_html .= '</div>';
$navbar_html .= '</nav>';


#Print the navbar
echo $navbar_html;

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