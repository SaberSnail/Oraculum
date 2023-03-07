# Design Document

## Workflows

### First Time Experience
1. See a list of sample tables
1. Select a sample table
1. Immediately see an example result
1. Hit a button to generate more results
1. Copy results to clipboard
1. Hit a button to change running parameters
    1. Choose how many times to run
    1. Choose whether or not to show rolls

## Features
* On startup restore last selected set and table
* Sets have a default grouping
* Set grouping can be overridden
* Tables have a default grouping
* Override table grouping when in a set
* Create a new set from a list of selected tables
* Can export one or more sets or tables
* Sets can include other sets or tables
* Tables can be dependent on other tables
* Hotkeys
    * Expand/collapse Sets list
    * Select opened sets (Ctrl+1 through Ctrl+0?)
    * Generate another result

## Panels

### Main Window
* Left side - collapsible list of sets
* Middle - tabbed list of open sets
    * An automatic "all tables" set is always open
    * Can drag to rearrange tab order
* Right side - output

### List of Sets
* Filterable
* Grouped
* Can select one or more
* Can rename or change grouping
* Import/export
* Load
* Collapsible, option to auto-collapse when loading set(s)

### Selected Set
* Set metadata
    * Click to edit most things in place
    * Most are not shown for default "all tables" set
* List of Tables
    * Filterable
    * Grouped
    * Can select one or more
    * Can rename or change grouping
    * Import/export

### Output
* Running output log
* Collapsible configuration options

